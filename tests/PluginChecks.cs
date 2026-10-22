// Made by PapaGordon
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WindowsGSM.Functions;
using WindowsGSM.Plugins;

internal static class PluginChecks
{
    private static int passed;

    private static void Check(bool condition, string description)
    {
        if (!condition)
            throw new Exception(description);
        passed++;
        Console.WriteLine("PASS " + description);
    }

    private static ServerConfig Settings(string id)
    {
        return new ServerConfig(id)
        {
            ServerName = "MeFriendos \"Enshrouded\" | Grüße",
            ServerIP = "0.0.0.0",
            ServerPort = "15636",
            ServerQueryPort = "15637",
            ServerMaxPlayer = "16",
            ServerParam = "/d /c exit 0"
        };
    }

    private static string ConfigPath(ServerConfig settings)
    {
        return ServerPath.GetServersServerFiles(settings.ServerID, "enshrouded_server.json");
    }

    private static JObject Read(ServerConfig settings)
    {
        return ReadJson(ConfigPath(settings));
    }

    private static JObject ReadJson(string path)
    {
        using (var text = File.OpenText(path))
        using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None })
            return JObject.Load(reader);
    }

    private static async Task Checks(string firstId, string secondId)
    {
        var settings = Settings(firstId);
        var plugin = new Enshrouded(settings);
        plugin.CreateServerCFG();
        Check(plugin.Error == null && File.Exists(ConfigPath(settings)), "Creates the first config against the real WindowsGSM API");
        JObject initial = Read(settings);
        Check((string)initial["name"] == settings.ServerName, "JSON keeps quotes and Unicode in the server name");
        Check((int)initial["queryPort"] == 15637 && (int)initial["slotCount"] == 16, "Query port and slots are JSON numbers");
        var groups = (JArray)initial["userGroups"];
        Check(groups.Count == 4 && groups.Select(g => (string)g["password"]).Distinct().Count() == 4
            && groups.All(g => ((string)g["password"]).Length >= 24), "Four roles have separate generated passwords");
        Check((bool)groups[0]["canKickBan"] && !(bool)groups[1]["canKickBan"]
            && !(bool)groups[3]["canEditWorld"], "Default role permissions match the reference");

        string original = File.ReadAllText(ConfigPath(settings));
        plugin.CreateServerCFG();
        Check(original == File.ReadAllText(ConfigPath(settings)), "Repeated config creation leaves existing data byte-for-byte unchanged");

        JObject custom = Read(settings);
        custom["userGroups"][0]["password"] = "Existing admin password";
        custom["userGroups"][1]["reservedSlots"] = 2;
        custom["saveDirectory"] = "D:/Worlds/Enshrouded";
        custom["logDirectory"] = "./custom-logs";
        custom["gameSettingsPreset"] = "Custom";
        custom["gameSettings"]["playerHealthFactor"] = 1.5;
        custom["tags"] = new JArray("German");
        custom["bans"] = new JArray("test-account");
        custom["futureSetting"] = new JObject { ["value"] = 123, ["dateText"] = "2026-10-22T12:34:56+02:00" };
        File.WriteAllText(ConfigPath(settings), custom.ToString());

        string serverFiles = ServerPath.GetServersServerFiles(settings.ServerID);
        // A short-lived Windows executable checks launch handling without installing a game.
        File.Copy(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"),
            Path.Combine(serverFiles, plugin.StartPath));
        settings.ServerName = "Changed server";
        settings.ServerIP = "127.0.0.1";
        settings.ServerQueryPort = "20000";
        settings.ServerMaxPlayer = "8";
        plugin.AllowsEmbedConsole = true;
        using (Process process = await plugin.Start())
        {
            Check(process != null, "Starts a fixture process with Embed Console enabled");
            process.WaitForExit();
            Check(process.StartInfo.RedirectStandardOutput && process.StartInfo.RedirectStandardError
                && !process.StartInfo.RedirectStandardInput, "Embedded output keeps native stdin available");
            Check(process.StartInfo.WindowStyle == ProcessWindowStyle.Hidden
                && process.StartInfo.WorkingDirectory == serverFiles, "Embedded mode uses the server directory and hidden native window");
            Check(process.StartInfo.Arguments == settings.ServerParam, "Extra parameters are passed through without synthetic setting overrides");
            await plugin.Stop(process);
        }

        JObject updated = Read(settings);
        Check((string)updated["name"] == "Changed server" && (string)updated["ip"] == "127.0.0.1"
            && (int)updated["queryPort"] == 20000 && (int)updated["slotCount"] == 8, "Start synchronizes all four WindowsGSM settings");
        Check(JToken.DeepEquals(updated["userGroups"], custom["userGroups"]), "Start preserves passwords, permissions and reserved slots");
        Check(JToken.DeepEquals(updated["gameSettings"], custom["gameSettings"])
            && (string)updated["gameSettingsPreset"] == "Custom", "Start preserves custom world settings");
        Check((string)updated["saveDirectory"] == (string)custom["saveDirectory"]
            && (string)updated["logDirectory"] == (string)custom["logDirectory"]
            && JToken.DeepEquals(updated["tags"], custom["tags"])
            && JToken.DeepEquals(updated["bans"], custom["bans"])
            && JToken.DeepEquals(updated["futureSetting"], custom["futureSetting"]), "Start preserves paths, tags, bans and unknown fields");
        Check(JToken.DeepEquals(ReadJson(ConfigPath(settings) + ".bak"), custom), "Atomic replacement backs up the previous config");
        Check(File.ReadAllText(ConfigPath(settings)).Contains("2026-10-22T12:34:56+02:00"), "Unknown date-shaped strings keep their original text");
        Check(Directory.GetFiles(serverFiles, "*.tmp").Length == 0, "Successful config update leaves no temporary files");

        string stable = File.ReadAllText(ConfigPath(settings));
        string backup = File.ReadAllText(ConfigPath(settings) + ".bak");
        plugin.AllowsEmbedConsole = false;
        using (Process process = await plugin.Start())
        {
            Check(process != null && !process.StartInfo.RedirectStandardOutput && !process.StartInfo.RedirectStandardError
                && process.StartInfo.WindowStyle == ProcessWindowStyle.Minimized, "Native mode leaves process output attached and starts minimized");
            process.WaitForExit();
        }
        Check(stable == File.ReadAllText(ConfigPath(settings)) && backup == File.ReadAllText(ConfigPath(settings) + ".bak"),
            "Unchanged settings leave the config and backup untouched");

        settings.ServerQueryPort = "0";
        Check(await plugin.Start() == null && plugin.Error.Contains("Query Port"), "Rejects query port zero");
        settings.ServerQueryPort = "65536";
        Check(await plugin.Start() == null && plugin.Error.Contains("Query Port"), "Rejects an out-of-range query port");
        settings.ServerQueryPort = "abc";
        Check(await plugin.Start() == null && plugin.Error.Contains("Query Port"), "Rejects a nonnumeric query port");
        settings.ServerQueryPort = "20000";
        settings.ServerMaxPlayer = "17";
        Check(await plugin.Start() == null && plugin.Error.Contains("Max Players"), "Rejects more than sixteen players");
        settings.ServerMaxPlayer = "0";
        Check(await plugin.Start() == null && plugin.Error.Contains("Max Players"), "Rejects zero players");
        settings.ServerMaxPlayer = "8";
        settings.ServerIP = "::1";
        Check(await plugin.Start() == null && plugin.Error.Contains("IPv4"), "Rejects IPv6");
        settings.ServerIP = "not-an-ip";
        Check(await plugin.Start() == null && plugin.Error.Contains("IPv4"), "Rejects an invalid bind address");
        settings.ServerIP = "127.0.0.1";
        settings.ServerName = " ";
        Check(await plugin.Start() == null && plugin.Error.Contains("Name"), "Rejects an empty name");
        settings.ServerName = "Changed server";
        Check(stable == File.ReadAllText(ConfigPath(settings)), "Invalid WindowsGSM settings never rewrite the config");

        File.WriteAllText(ConfigPath(settings), "{ invalid json");
        Check(await plugin.Start() == null && plugin.Error.Contains("enshrouded_server.json"), "Malformed JSON blocks startup with a useful error");
        Check(File.ReadAllText(ConfigPath(settings)) == "{ invalid json", "Malformed JSON is not overwritten");
        File.WriteAllText(ConfigPath(settings), stable + " {}");
        Check(await plugin.Start() == null, "Rejects additional JSON content after the config object");
        File.WriteAllText(ConfigPath(settings), stable);
        File.SetAttributes(ConfigPath(settings), FileAttributes.ReadOnly);
        settings.ServerName = "Read-only update";
        try
        {
            Check(await plugin.Start() == null && plugin.Error.Contains("enshrouded_server.json"), "Config write failure blocks startup");
            Check(stable == File.ReadAllText(ConfigPath(settings)) && Directory.GetFiles(serverFiles, "*.tmp").Length == 0,
                "Failed config replacement keeps the original and cleans temporary files");
        }
        finally
        {
            File.SetAttributes(ConfigPath(settings), FileAttributes.Normal);
        }

        File.Delete(ConfigPath(settings));
        using (Process process = await plugin.Start())
        {
            Check(process != null && plugin.Error == null && File.Exists(ConfigPath(settings)), "Start recreates a missing config and clears earlier errors");
            process.WaitForExit();
        }

        var secondSettings = Settings(secondId);
        var second = new Enshrouded(secondSettings);
        second.CreateServerCFG();
        Check(!JToken.DeepEquals(Read(settings)["userGroups"], Read(secondSettings)["userGroups"]), "Two server instances keep separate passwords and config paths");
        Check(await second.Start() == null && second.Error.Contains("File not found"), "Missing server executable gives a useful error");
        await second.Stop(null);
        Check(second.AppId == "2278520" && second.loginAnonymous && second.QueryMethod != null, "Uses the Enshrouded Steam app, anonymous login and A2S query");
    }

    private static int Main()
    {
        // The real ServerPath resolves beneath this test executable, never the user's installation.
        string suffix = Guid.NewGuid().ToString("N");
        string firstId = "plugin-check-" + suffix;
        string secondId = firstId + "-second";
        try
        {
            Checks(firstId, secondId).GetAwaiter().GetResult();
            Console.WriteLine($"{passed} checks passed.");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
        finally
        {
            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "servers"));
            foreach (string id in new[] { firstId, secondId })
            {
                string path = Path.GetFullPath(ServerPath.GetServers(id));
                if (path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && Directory.Exists(path))
                    Directory.Delete(path, true);
            }
        }
    }
}
