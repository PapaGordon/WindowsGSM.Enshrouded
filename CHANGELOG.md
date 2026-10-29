# Changelog

## 0.1.1 - 2026-10-29

- Refreshes the README links, current WindowsGSM guidance and Steam credential compatibility notes.
- Uses the current canonical repository URL. Server runtime behavior is unchanged.


## 0.1.0 - 2026-10-22

- Added the MeFriendos build based on the ohmcodes Enshrouded plugin.
- Added a compact Enshrouded insignia for the server list and a separate MF author icon, both with transparent backgrounds.
- Added live process output for the WindowsGSM embedded console with native stdin kept available for CTRL+C shutdown.
- Added IPv4, query port and player count validation before startup.
- Synchronizes the WindowsGSM name, IP, query port and player count through `enshrouded_server.json`.
- Preserves existing passwords, permissions, world settings, paths and unknown JSON fields.
- Generates separate random passwords for Admin, Friend, Guest and Visitor on first setup.
- Uses an atomic config replacement and keeps the previous version as `.json.bak` when settings change.
- Leaves firewall management entirely to WindowsGSM.
