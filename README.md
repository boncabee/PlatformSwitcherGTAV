# Grand Theft Auto V Platform Switcher (v2.0)

A modern **.NET 8 WinForms** utility for switching a single Grand Theft Auto V installation (**Legacy** and **Enhanced Edition**) between **Rockstar Games Launcher (RGL)**, **Steam**, and **Epic Games Launcher (EGL)** without duplicating the entire game directory.

## What's New in v2.0

- **.NET 8 WinForms Modernization**: Clean SDK-style project, `PerMonitorV2` High-DPI support, single-file `.exe` publishing, Dark & Light themes (including Windows 10/11 dark title bar), and JSON settings stored safely in `%AppData%\PlatformSwitcherGTAV\settings.json`.
- **GTA V Enhanced Edition & BattlEye Support**:
  - Dedicated profiles for both **GTA V Legacy** (`GTA5.exe`, `GTA5_BE.exe`, Steam AppID `271590`) and **GTA V Enhanced** (`GTA5_Enhanced.exe`, `GTA5_Enhanced_BE.exe`, Steam AppID `3240220`).
  - Zero-dependency BattlEye health inspection (`*_BE.exe`, `BattlEye\` folder, `BEService` status) and optional `-nobattleye` Story Mode / single-player launch toggle.
- **Doctor & First-Time Setup**:
  - Built-in diagnostics for game path & write permissions, active platform auto-detection heuristics, `Switcher\` profile readiness, `steam_api64.dll` / `EOSSDK-Win64-Shipping.dll` conflict repair, BattlEye health, registry consistency, and running game locks.
  - One-click fixes and **Copy Diagnostic Report** (Markdown).
- **Safe Atomic Switching & Version Mismatch Guard**:
  - Replaces `xcopy` + `Thread.Sleep` with async .NET file operations, write-access pre-checks, process-lock checks, and automatic rollback on failure.
  - Warns if a stored backup profile is older than the active game executable (e.g., after a patch) so you do not accidentally downgrade your game.
- **Auto-Detection Everywhere**:
  - Detects installations from portable directory, Windows Registry (`WOW6432Node`), Steam `libraryfolders.vdf` + `appmanifest_*.acf`, and Epic Games `.item` manifests.
- **CLI & Desktop Shortcuts + GitHub Update Checker**:
  - One-click Desktop Shortcut creation per edition/platform and CLI automation:
    ```powershell
    "Grand Theft Auto V Platform Switcher.exe" --edition enhanced --switch steam --launch
    ```
  - Built-in GitHub Releases update checker (`boncabee/PlatformSwitcherGTAV`).

## Profile Folder Layout

Platform binaries are stored inside your game folder under:
- **GTA V Legacy**: `<GameFolder>\Switcher\Legacy\<RGL|Steam|EGL>` *(automatically falls back to legacy `<GameFolder>\Switcher\<RGL|Steam|EGL>`)*
- **GTA V Enhanced**: `<GameFolder>\Switcher\Enhanced\<RGL|Steam|EGL>`

Use the **Backup Current** button on any platform card (or the **Doctor / Setup** wizard) to capture the active platform binaries automatically instead of copying files manually.

## Building from Source

Requires the **.NET 8 SDK** on Windows:

```powershell
# Build & run unit tests
dotnet test "Grand Theft Auto V Platform Switcher.sln" -c Release

# Publish framework-dependent single-file executable (requires .NET 8 Desktop Runtime)
dotnet publish "Grand Theft Auto V Platform Switcher.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```
