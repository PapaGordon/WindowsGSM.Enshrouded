// Made by PapaGordon
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Engine;
using WindowsGSM.GameServer.Query;

namespace WindowsGSM.Plugins
{
    // MeFriendos build based on WindowsGSM.Enshrouded by ohmcodes.
    public class Enshrouded : SteamCMDAgent
    {
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.Enshrouded",
            author = "MeFriendos",
            description = "WindowsGSM plugin for Enshrouded Dedicated Server (MeFriendos build)",
            version = "0.1.0",
            url = "https://github.com/PapaGordon/WindowsGSM.Enshrouded",
            color = "#34c9eb"
        };

        public override bool loginAnonymous => true;
        public override string AppId => "2278520";
        public override string StartPath => "enshrouded_server.exe";

        public Enshrouded(ServerConfig serverData) : base(serverData)
        {
            base.serverData = _serverData = serverData;
        }

        private readonly ServerConfig _serverData;
        private const string ConfigName = "enshrouded_server.json";
        private static readonly object ConsoleLock = new object();

        public string FullName = "Enshrouded Dedicated Server";
        public bool AllowsEmbedConsole = true;
        public int PortIncrements = 1;
        public object QueryMethod = new A2S();

        // Server Port is kept for the WindowsGSM UI; Enshrouded uses Query Port.
        public string Port = "15636";
        public string QueryPort = "15637";
        public string Defaultmap = "Dedicated";
        public string Maxplayers = "16";
        public string Additional = "-log";

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint processId);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern bool FreeConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GenerateConsoleCtrlEvent(uint ctrlEvent, uint processGroupId);

        private delegate bool ConsoleCtrlDelegate(uint ctrlType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate handler, bool add);

        public void CreateServerCFG()
        {
            Error = null;
            string configPath = ServerPath.GetServersServerFiles(_serverData.ServerID, ConfigName);
            if (File.Exists(configPath))
                return;

            PrepareConfiguration();
        }

        public Task<Process> Start()
        {
            Error = null;
            Notice = null;
            string serverFiles = ServerPath.GetServersServerFiles(_serverData.ServerID);
            string executable = Path.Combine(serverFiles, StartPath);

            if (!File.Exists(executable))
            {
                Error = $"File not found: {executable}";
                return Task.FromResult<Process>(null);
            }

            if (!PrepareConfiguration())
                return Task.FromResult<Process>(null);

            bool embedConsole = AllowsEmbedConsole;
            var process = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = serverFiles,
                    FileName = executable,
                    Arguments = _serverData.ServerParam ?? string.Empty,
                    WindowStyle = embedConsole ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Minimized,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };

            // Keep stdin on the native console so CTRL+C can reach the server.
            if (embedConsole)
            {
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                var serverConsole = new ServerConsole(_serverData.ServerID);
                process.OutputDataReceived += serverConsole.AddOutput;
                process.ErrorDataReceived += serverConsole.AddOutput;
            }

            try
            {
                process.Start();
                if (embedConsole)
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                }
                return Task.FromResult(process);
            }
            catch (Exception e)
            {
                Error = $"Could not start Enshrouded: {e.Message}";
                process.Dispose();
                return Task.FromResult<Process>(null);
            }
        }

        private bool PrepareConfiguration()
        {
            string configPath = ServerPath.GetServersServerFiles(_serverData.ServerID, ConfigName);
            try
            {
                int queryPort, slots;
                IPAddress address;
                if (string.IsNullOrWhiteSpace(_serverData.ServerName))
                    throw new InvalidOperationException("Server Name must not be empty.");

                if (!IPAddress.TryParse(_serverData.ServerIP, out address)
                    || address.AddressFamily != AddressFamily.InterNetwork)
                    throw new InvalidOperationException("Server IP must be an IPv4 address. Use 0.0.0.0 to listen on all interfaces.");

                if (!int.TryParse(_serverData.ServerQueryPort, NumberStyles.None, CultureInfo.InvariantCulture, out queryPort)
                    || queryPort < 1 || queryPort > 65535)
                    throw new InvalidOperationException("Server Query Port must be a number between 1 and 65535.");

                if (!int.TryParse(_serverData.ServerMaxPlayer, NumberStyles.None, CultureInfo.InvariantCulture, out slots)
                    || slots < 1 || slots > 16)
                    throw new InvalidOperationException("Server Max Players must be a number between 1 and 16.");

                bool exists = File.Exists(configPath);
                JObject config;
                if (exists)
                {
                    // Keep date-shaped strings unchanged when saving custom fields.
                    using (var text = File.OpenText(configPath))
                    using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None })
                    {
                        config = JObject.Load(reader);
                        while (reader.Read())
                        {
                            if (reader.TokenType != JsonToken.Comment)
                                throw new JsonReaderException("Unexpected content after the config object.");
                        }
                    }
                }
                else
                {
                    config = CreateDefaultConfiguration();
                }
                JObject previous = (JObject)config.DeepClone();

                config["name"] = _serverData.ServerName;
                config["ip"] = address.ToString();
                config["queryPort"] = queryPort;
                config["slotCount"] = slots;

                if (!exists || !JToken.DeepEquals(previous, config))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(configPath));
                    WriteConfiguration(configPath, config, exists);
                }

                return true;
            }
            catch (Exception e)
            {
                Error = $"Could not prepare {ConfigName}: {e.Message}";
                return false;
            }
        }

        private static void WriteConfiguration(string configPath, JObject config, bool exists)
        {
            string temporaryPath = configPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, config.ToString(Formatting.Indented) + Environment.NewLine, new UTF8Encoding(false));
                if (exists)
                    File.Replace(temporaryPath, configPath, configPath + ".bak");
                else
                    File.Move(temporaryPath, configPath);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        private static JObject CreateDefaultConfiguration()
        {
            return JObject.FromObject(new
            {
                saveDirectory = "./savegame",
                logDirectory = "./logs",
                tags = new string[] { },
                voiceChatMode = "Proximity",
                enableVoiceChat = false,
                enableTextChat = false,
                gameSettingsPreset = "Default",
                gameSettings = new
                {
                    playerHealthFactor = 1,
                    playerManaFactor = 1,
                    playerStaminaFactor = 1,
                    playerBodyHeatFactor = 1,
                    playerDivingTimeFactor = 1,
                    enableDurability = true,
                    enableStarvingDebuff = false,
                    foodBuffDurationFactor = 1,
                    fromHungerToStarving = 600000000000L,
                    shroudTimeFactor = 1,
                    tombstoneMode = "AddBackpackMaterials",
                    enableGliderTurbulences = true,
                    weatherFrequency = "Normal",
                    fishingDifficulty = "Normal",
                    miningDamageFactor = 1,
                    plantGrowthSpeedFactor = 1,
                    resourceDropStackAmountFactor = 1,
                    factoryProductionSpeedFactor = 1,
                    perkUpgradeRecyclingFactor = 0.5,
                    perkCostFactor = 1,
                    experienceCombatFactor = 1,
                    experienceMiningFactor = 1,
                    experienceExplorationQuestsFactor = 1,
                    randomSpawnerAmount = "Normal",
                    aggroPoolAmount = "Normal",
                    enemyDamageFactor = 1,
                    enemyHealthFactor = 1,
                    enemyStaminaFactor = 1,
                    enemyPerceptionRangeFactor = 1,
                    bossDamageFactor = 1,
                    bossHealthFactor = 1,
                    threatBonus = 1,
                    pacifyAllEnemies = false,
                    tamingStartleRepercussion = "LoseSomeProgress",
                    dayTimeDuration = 1800000000000L,
                    nightTimeDuration = 720000000000L,
                    curseModifier = "Normal"
                },
                userGroups = new[]
                {
                    CreateGroup("Admin", true, true, true, true, true),
                    CreateGroup("Friend", false, true, true, true, false),
                    CreateGroup("Guest", false, false, true, false, false),
                    CreateGroup("Visitor", false, false, false, false, false)
                },
                bans = new string[] { }
            });
        }

        private static JObject CreateGroup(string name, bool kickBan, bool inventories, bool world, bool editBase, bool extendBase)
        {
            byte[] bytes = new byte[12];
            using (var random = RandomNumberGenerator.Create())
                random.GetBytes(bytes);

            return new JObject
            {
                ["name"] = name,
                ["password"] = name + BitConverter.ToString(bytes).Replace("-", string.Empty),
                ["canKickBan"] = kickBan,
                ["canAccessInventories"] = inventories,
                ["canEditWorld"] = world,
                ["canEditBase"] = editBase,
                ["canExtendBase"] = extendBase,
                ["reservedSlots"] = 0
            };
        }

        public async Task Stop(Process process)
        {
            if (process == null)
                return;

            await Task.Run(() =>
            {
                if (TrySendCtrlC(process, 30000))
                    return;

                try
                {
                    process.Refresh();
                    if (!process.HasExited && process.MainWindowHandle != IntPtr.Zero)
                    {
                        ServerConsole.SetMainWindow(process.MainWindowHandle);
                        ServerConsole.SendWaitToMainWindow("^c");
                        if (process.WaitForExit(10000))
                            return;
                    }
                }
                catch (InvalidOperationException)
                {
                    return;
                }

                Notice = "Enshrouded did not exit after CTRL+C. WindowsGSM may force termination; check the server log before restarting.";
            });
        }

        private static bool TrySendCtrlC(Process process, int timeoutMilliseconds)
        {
            // Console attachment is process-wide, including when two instances stop at once.
            lock (ConsoleLock)
            {
                bool attached = false;
                bool ignoringCtrlC = false;
                try
                {
                    if (process.HasExited)
                        return true;

                    attached = AttachConsole((uint)process.Id);
                    if (!attached)
                        return false;

                    ignoringCtrlC = SetConsoleCtrlHandler(null, true);
                    if (!ignoringCtrlC || !GenerateConsoleCtrlEvent(0, 0))
                        return false;

                    return process.WaitForExit(timeoutMilliseconds);
                }
                catch (InvalidOperationException)
                {
                    return true;
                }
                finally
                {
                    if (attached)
                        FreeConsole();
                    if (ignoringCtrlC)
                        SetConsoleCtrlHandler(null, false);
                }
            }
        }
    }
}
