# Platform Switcher GTA V

Switch **GTA V** (**GTA 5**, **Grand Theft Auto V**, Legacy and Enhanced Edition) between **Rockstar Games Launcher**, **Steam**, and **Epic Games** with one game folder, no duplicate install.

## Features

- Auto-detects Rockstar, Steam, and Epic installs (registry, Steam manifests, Epic manifests).
- Separate profiles for GTA V Legacy and GTA V Enhanced Edition.
- Safe switching with lock and write checks, plus automatic rollback on failure.
- Doctor diagnostics with one-click fixes and a copyable report.
- BattlEye health check and `-nobattleye` toggle for Story Mode.
- Command-line interface and desktop shortcuts.

## Download

Get the latest build from [Releases](https://github.com/boncabee/PlatformSwitcherGTAV/releases/latest).
Requires Windows and the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0).

## Usage

1. Run `Platform Switcher GTA V.exe` and pick your edition (Legacy or Enhanced).
2. Click **Backup Current** on the card of the platform you are using now (or run **Doctor / Setup**).
3. Repeat step 2 for each other platform after it is installed once.
4. Click a platform card to switch, then launch the game.

CLI example:

```powershell
"Platform Switcher GTA V.exe" --edition enhanced --switch steam --launch
```

Options: `--edition <legacy|enhanced>`, `--switch <rgl|steam|egl>`, `--launch`, `--nobattleye`, `--battleye`, `--help`.

## Profile folder layout

Platform files are stored inside your game folder:

- Legacy: `<GameFolder>\Switcher\Legacy\<RGL|Steam|EGL>` (falls back to `<GameFolder>\Switcher\<RGL|Steam|EGL>`)
- Enhanced: `<GameFolder>\Switcher\Enhanced\<RGL|Steam|EGL>`

## Build from source

Requires the .NET 8 SDK on Windows.

```powershell
dotnet test "Platform Switcher GTA V.sln" -c Release
dotnet publish "Platform Switcher GTA V.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

## Credits

Original project by TechnoStone. Maintained by [boncabee](https://github.com/boncabee).

## License

[MIT](LICENSE)

## Disclaimer

Unofficial tool. Not affiliated with or endorsed by Rockstar Games, Take-Two Interactive, Valve, or Epic Games.
