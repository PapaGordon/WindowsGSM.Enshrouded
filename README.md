<p align="center">
  <img src="Enshrouded.cs/Enshrouded.png" alt="Enshrouded" width="160">
</p>

<h1 align="center">WindowsGSM.Enshrouded</h1>

<p align="center">
  MeFriendos build for running an Enshrouded dedicated server with WindowsGSM.
</p>

<p align="center">
  <a href="https://github.com/PapaGordon/WindowsGSM/releases/tag/v1.25.3.0"><img src="https://img.shields.io/badge/WindowsGSM-MeFriendos%20v1.25.3.0-38CDD4" alt="MeFriendos WindowsGSM v1.25.3.0"></a>
  <a href="CHANGELOG.md"><img src="https://img.shields.io/badge/version-0.1.0-34c9eb" alt="Version 0.1.0"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="MIT License"></a>
</p>

Based on the Enshrouded plugin by [ohmcodes](https://github.com/ohmcodes/WindowsGSM.Enshrouded), with the same MeFriendos layout and console handling as our other WindowsGSM plugins.

## Features

- Uses a compact Enshrouded insignia for the server list and a separate MeFriendos author icon.
- Installs and updates the official dedicated server through SteamCMD with anonymous login.
- Uses A2S for WindowsGSM server queries.
- Forwards stdout and stderr to the embedded console while keeping native stdin available for CTRL+C.
- Starts the native console hidden with Embed Console enabled, or minimized with it disabled.
- Sends CTRL+C and waits up to 30 seconds when stopping, with a native-window fallback.
- Validates the bind address, query port and player count before starting.
- Creates separate random passwords for Admin, Friend, Guest and Visitor on first setup.
- Preserves existing passwords, roles, world settings, save paths and unknown JSON fields.
- Leaves all firewall management to WindowsGSM.

## Quick overview

| Setting | Value |
| --- | --- |
| SteamCMD App ID | `2278520` |
| Executable | `enshrouded_server.exe` |
| Configuration | `enshrouded_server.json` in the server root |
| Query / connection port | `15637/UDP` |
| WindowsGSM Server Port | `15636`, retained from the reference plugin |
| Port increment | `1` |
| Player slots | `1` to `16`, default `16` |
| Default parameters | `-log`, retained from the reference plugin |
| SteamCMD login | Anonymous |
| Embedded console | Read-only process output |

Enshrouded uses `queryPort` in its configuration. Set **Server Query Port** in WindowsGSM to change it. The separate **Server Port** and **Server Map** fields do not configure an Enshrouded port or world.

## Installation

MeFriendos uses this plugin with [WindowsGSM v1.25.3.0](https://github.com/PapaGordon/WindowsGSM/releases/tag/v1.25.3.0). The Windows x64 download includes the .NET runtime.

1. Download [MeFriendos WindowsGSM v1.25.3.0](https://github.com/PapaGordon/WindowsGSM/releases/tag/v1.25.3.0) for 64-bit Windows. It includes firewall management.
2. Copy the complete `Enshrouded.cs` folder into `<WindowsGSM>\plugins\`.
3. Click **Reload Plugins** or restart WindowsGSM.
4. Add **Enshrouded Dedicated Server** and run **Install**.
5. Set the server name, IPv4 bind address, **Server Query Port** and **Server Max Players** in WindowsGSM. Use `0.0.0.0` to listen on all interfaces.
6. While the server is stopped, open `servers\<id>\serverfiles\enshrouded_server.json`. Review the generated role passwords and permissions before sharing access.
7. Set **Auto Firewall** in WindowsGSM as needed and configure any router forwarding for the selected query port.
8. Start the server and review the console or `logs` folder.

Game binaries are downloaded by SteamCMD and are not included in this repository. The supplied files in `referenz` are kept as reference material; install only the `Enshrouded.cs` folder.

## Configuration and passwords

Before every start the plugin updates only these values from WindowsGSM:

| WindowsGSM field | JSON field |
| --- | --- |
| Server Name | `name` |
| Server IP | `ip` |
| Server Query Port | `queryPort` |
| Server Max Players | `slotCount` |

Edit all other settings directly in `enshrouded_server.json` while the server is stopped. Existing `userGroups`, passwords, reserved slots, bans, chat settings, game settings and save/log directories remain as configured. Additional startup parameters are passed to the executable as entered; the plugin does not add command-line overrides for these four settings.

The initial configuration follows the supplied reference, including its four role presets and default world settings. Each role gets its own random password. Passwords stay in the JSON file and are never printed by the plugin. The password used to join selects the role, so share the Friend password with normal players and keep the Admin password private. Existing legacy configurations with a top-level password are preserved for the game's own migration.

Repeated **CreateServerCFG** calls leave an existing file untouched. Startup refuses invalid JSON or invalid WindowsGSM settings. When one of the four managed values changes, the plugin writes a temporary file and atomically replaces the config, keeping the previous file as `enshrouded_server.json.bak`. An unchanged configuration is not rewritten. The backup also contains role passwords.

This config backup does not replace a world backup. Back up the config and the configured save directory before game updates. If you delete the config, the next setup/start creates new role passwords.

## Console and shutdown

WindowsGSM sets the effective Embed Console state before calling the plugin. With it enabled, stdout and stderr are redirected into WindowsGSM and the native console starts hidden. **Toggle Console** is disabled by WindowsGSM while stdout is redirected. Disable Embed Console to use the native window.

The embedded output is read-only. Standard input stays on the native console for shutdown. Stop first attaches to the server console, sends CTRL+C and waits up to 30 seconds. If attachment fails or the wait expires, it tries CTRL+C through an available native window and waits another 10 seconds. WindowsGSM controls the final forced termination if the server still does not exit.

## Firewall

This plugin does not create, remove or inspect firewall rules and does not require elevation for a firewall check. Firewall behavior follows WindowsGSM's per-server **Auto Firewall** setting. When using manual rules, allow the selected query port and any other traffic required by your network setup. Router forwarding remains a separate step.

## Validation

The checks in `tests` compile the real plugin against a built WindowsGSM installation. They exercise config generation and preservation, atomic replacement and write failures, invalid settings, separate server instances and both console modes using a short-lived Windows test process. They do not download or start the Enshrouded game server.

```powershell
dotnet run --project tests/PluginChecks.csproj -c Release -p:WindowsGSMDirectory="D:\path\to\WindowsGSM"
```

The directory must contain `WindowsGSM.dll`, `Newtonsoft.Json.dll` and the WindowsGSM runtime dependencies. The default path points to the sibling WindowsGSM checkout's Release build. Test config files are isolated beneath the test executable and removed after the run.

Local validation on 2026-10-22:

- Release build with the installed .NET 10 SDK: no warnings or errors. With a newer SDK, set `-p:PluginCheckTargetFramework=net10.0-windows8.0` to use its targeting pack.
- All 37 checks passed on .NET 10 and again on .NET 8.0.29 against the real WindowsGSM assembly.
- The .NET 8 compatibility run was compiled directly against the installed runtime assemblies because the .NET 8 targeting pack was unavailable. That compilation reported one `CS1701` assembly-version warning for Newtonsoft.Json's .NET 6 reference to `System.Linq.Expressions`; the .NET 8 run passed.

The automated checks do not cover SteamCMD install/update, Enshrouded startup, A2S responses, client connections, live log output or a completed world save during CTRL+C shutdown.

## Links

- [MeFriendos WindowsGSM v1.25.3.0](https://github.com/PapaGordon/WindowsGSM/releases/tag/v1.25.3.0)
- [Original plugin](https://github.com/ohmcodes/WindowsGSM.Enshrouded)
- [Official dedicated server configuration](https://enshrouded.zendesk.com/hc/en-us/articles/16055441447709-Dedicated-Server-Configuration)
- [Official server roles configuration](https://enshrouded.zendesk.com/hc/en-us/articles/19191581489309-Server-Roles-Configuration)
- [Official dedicated server FAQ](https://enshrouded.zendesk.com/hc/en-us/articles/16056312924957-Dedicated-Server-FAQ)
- [MeFriendos community](https://mefriendos.de)

## License

Made by PapaGordon for MeFriendos. Based on the original plugin by ohmcodes. The original MIT notice is retained in [LICENSE](LICENSE).
