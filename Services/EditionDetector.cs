using System.Text.Json;
using System.Text.RegularExpressions;
using Grand_Theft_Auto_V_Platform_Switcher.Models;
using Microsoft.Win32;

namespace Grand_Theft_Auto_V_Platform_Switcher.Services;

public sealed record EpicManifestInfo(string AppName, string DisplayName, string InstallLocation);

public sealed record DetectionResult(string? GameDirectory, string? EpicAppName, string Source);

public sealed class EditionDetector
{
    private readonly AppLogger _logger;
    private readonly string _appBaseDirectory;
    private readonly string _epicManifestsDirectory;
    private readonly bool _disableSystemAutoDetect;

    internal string EpicManifestsDirectory => _epicManifestsDirectory;

    public EditionDetector(
        AppLogger logger,
        string? appBaseDirectory = null,
        string? epicManifestsDirectory = null,
        bool disableSystemAutoDetect = false)
    {
        _logger = logger;
        _appBaseDirectory = string.IsNullOrWhiteSpace(appBaseDirectory)
            ? AppContext.BaseDirectory
            : appBaseDirectory;
        _epicManifestsDirectory = string.IsNullOrWhiteSpace(epicManifestsDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Epic",
                "EpicGamesLauncher",
                "Data",
                "Manifests")
            : epicManifestsDirectory;
        _disableSystemAutoDetect = disableSystemAutoDetect;
    }

    public bool IsValidGameDirectory(EditionSpec edition, string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        return File.Exists(Path.Combine(directory, edition.MainExecutable));
    }

    public DetectionResult DetectInstallation(EditionSpec edition)
    {
        // 1. Portable check: app placed next to GTA5.exe / GTA5_Enhanced.exe
        if (IsValidGameDirectory(edition, _appBaseDirectory))
        {
            var epicApp = FindEpicAppName(edition, _appBaseDirectory);
            return new DetectionResult(_appBaseDirectory.TrimEnd('\\', '/'), epicApp, "Portable directory");
        }

        if (!_disableSystemAutoDetect)
        {
            // 2. Windows Registry (Rockstar Games Launcher)
            var regDir = TryFindFromRegistry(edition);
            if (IsValidGameDirectory(edition, regDir))
            {
                var epicApp = FindEpicAppName(edition, regDir);
                return new DetectionResult(regDir, epicApp, "Windows Registry");
            }

            // 3. Steam libraryfolders.vdf + appmanifest_<AppId>.acf
            var steamDir = TryFindFromSteamLibraries(edition);
            if (IsValidGameDirectory(edition, steamDir))
            {
                var epicApp = FindEpicAppName(edition, steamDir);
                return new DetectionResult(steamDir, epicApp, "Steam Library");
            }
        }

        // 4. Epic Games Launcher .item manifests
        var epicManifest = TryFindEpicManifest(edition, null);
        if (epicManifest is not null && IsValidGameDirectory(edition, epicManifest.InstallLocation))
        {
            return new DetectionResult(epicManifest.InstallLocation, epicManifest.AppName, "Epic Games Manifest");
        }

        return new DetectionResult(null, epicManifest?.AppName, "Not found");
    }

    public string? FindEpicAppName(EditionSpec edition, string? gameDirectory)
    {
        var manifest = TryFindEpicManifest(edition, gameDirectory);
        if (manifest is not null && !string.IsNullOrWhiteSpace(manifest.AppName))
        {
            return manifest.AppName;
        }

        return string.IsNullOrWhiteSpace(edition.DefaultEpicAppId) ? null : edition.DefaultEpicAppId;
    }

    public GamePlatform DetectActivePlatform(EditionSpec edition, string? gameDirectory)
    {
        if (!IsValidGameDirectory(edition, gameDirectory) || gameDirectory is null)
        {
            return GamePlatform.Unknown;
        }

        var hasSteamApi = File.Exists(Path.Combine(gameDirectory, "steam_api64.dll"));
        if (hasSteamApi)
        {
            return GamePlatform.Steam;
        }

        var hasEosSdk = PlatformSwitchService.HasActiveEpicSdk(gameDirectory);
        if (hasEosSdk)
        {
            return GamePlatform.EGL;
        }

        // .egstore persists in the game folder even after switching to RGL, so check RGL markers first.
        if (PlatformSwitchService.HasParkedEpicSdk(gameDirectory) ||
            File.Exists(Path.Combine(gameDirectory, "title.rgl")) ||
            File.Exists(Path.Combine(gameDirectory, "PlayGTAV.exe")))
        {
            return GamePlatform.RGL;
        }

        if (Directory.Exists(Path.Combine(gameDirectory, ".egstore")))
        {
            return GamePlatform.EGL;
        }

        return GamePlatform.Unknown;
    }

    public string GetPrimaryProfileDirectory(EditionSpec edition, string gameDirectory, GamePlatform platform)
    {
        var platformFolder = EditionSpec.GetPlatformFolderName(platform);
        // Portable layout: backups live next to the application executable.
        return Path.Combine(_appBaseDirectory, "Switcher", edition.ProfileFolderName, platformFolder);
    }

    public string ResolveProfileDirectory(EditionSpec edition, string gameDirectory, GamePlatform platform)
    {
        var platformFolder = EditionSpec.GetPlatformFolderName(platform);

        // Preferred portable layout: <AppDirectory>\Switcher\<Edition>\<Platform>
        var primary = Path.Combine(_appBaseDirectory, "Switcher", edition.ProfileFolderName, platformFolder);
        if (HasProfileExecutable(edition, primary))
        {
            return primary;
        }

        // Older layout (migration): <GameDirectory>\Switcher\<Edition>\<Platform>
        var gameEditionFallback = Path.Combine(gameDirectory, "Switcher", edition.ProfileFolderName, platformFolder);
        if (HasProfileExecutable(edition, gameEditionFallback))
        {
            return gameEditionFallback;
        }

        // Backward-compatible Legacy layout: <GameDirectory>\Switcher\<Platform>
        if (edition.Edition == GameEdition.Legacy)
        {
            var legacyFallback = Path.Combine(gameDirectory, "Switcher", platformFolder);
            if (HasProfileExecutable(edition, legacyFallback))
            {
                return legacyFallback;
            }
        }

        // App-adjacent fallback if user stores Switcher\ next to the switcher .exe
        var appEditionFallback = Path.Combine(_appBaseDirectory, "Switcher", edition.ProfileFolderName, platformFolder);
        if (HasProfileExecutable(edition, appEditionFallback))
        {
            return appEditionFallback;
        }

        if (edition.Edition == GameEdition.Legacy)
        {
            var appLegacyFallback = Path.Combine(_appBaseDirectory, "Switcher", platformFolder);
            if (HasProfileExecutable(edition, appLegacyFallback))
            {
                return appLegacyFallback;
            }
        }

        return primary;
    }

    private static bool HasProfileExecutable(EditionSpec edition, string directory) =>
        Directory.Exists(directory) && File.Exists(Path.Combine(directory, edition.MainExecutable));

    private string? TryFindFromRegistry(EditionSpec edition)
    {
        var subKeys = new[] { edition.EpicAndRglRegistrySubKey, edition.SteamRegistrySubKey }.Distinct();
        var valueNames = new[] { "InstallFolder", "InstallFolderSteam", "InstallFolderEpic" };

        foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
        {
            try
            {
                using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                foreach (var subKeyPath in subKeys)
                {
                    using var key = hklm.OpenSubKey(subKeyPath, writable: false);
                    if (key is null)
                    {
                        continue;
                    }

                    foreach (var valueName in valueNames)
                    {
                        if (key.GetValue(valueName) is string rawPath && !string.IsNullOrWhiteSpace(rawPath))
                        {
                            var normalized = rawPath.Trim().Trim('"');
                            if (IsValidGameDirectory(edition, normalized))
                            {
                                return normalized;
                            }

                            // Handle legacy bug where "\GTAV" was appended to InstallFolderSteam.
                            var parent = Path.GetDirectoryName(normalized);
                            if (IsValidGameDirectory(edition, parent))
                            {
                                return parent;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"Registry detection ({view}) failed for {edition.DisplayName}: {ex.Message}");
            }
        }

        return null;
    }

    private string? TryFindFromSteamLibraries(EditionSpec edition)
    {
        var steamRoots = new List<string>();
        try
        {
            using var steamKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam", writable: false);
            if (steamKey?.GetValue("SteamPath") is string steamPath && !string.IsNullOrWhiteSpace(steamPath))
            {
                steamRoots.Add(steamPath.Replace('/', '\\'));
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"Could not read HKCU SteamPath: {ex.Message}");
        }

        var defaultSteam = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Steam");
        if (Directory.Exists(defaultSteam))
        {
            steamRoots.Add(defaultSteam);
        }

        foreach (var steamRoot in steamRoots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var found = FindGameInSteamRoot(edition, steamRoot);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    internal string? FindGameInSteamRoot(EditionSpec edition, string steamRoot)
    {
        var libraries = new List<string> { steamRoot };
        var vdfPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdfPath))
        {
            try
            {
                var vdfContent = File.ReadAllText(vdfPath);
                libraries.AddRange(ParseSteamLibraryFolders(vdfContent));
            }
            catch (Exception ex)
            {
                _logger.Warn($"Failed to parse {vdfPath}: {ex.Message}");
            }
        }

        foreach (var lib in libraries.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var manifestPath = Path.Combine(lib, "steamapps", $"appmanifest_{edition.SteamAppId}.acf");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            try
            {
                var installDir = ParseSteamAppManifestInstallDir(File.ReadAllText(manifestPath));
                if (!string.IsNullOrWhiteSpace(installDir))
                {
                    var fullPath = Path.Combine(lib, "steamapps", "common", installDir);
                    if (IsValidGameDirectory(edition, fullPath))
                    {
                        return fullPath;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"Failed to parse {manifestPath}: {ex.Message}");
            }
        }

        return null;
    }

    internal static IReadOnlyList<string> ParseSteamLibraryFolders(string vdfContent)
    {
        var results = new List<string>();
        var matches = Regex.Matches(vdfContent, "\"path\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
        foreach (Match match in matches)
        {
            var raw = match.Groups[1].Value.Replace(@"\\", @"\").Trim();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                results.Add(raw);
            }
        }

        return results;
    }

    internal static string? ParseSteamAppManifestInstallDir(string acfContent)
    {
        var match = Regex.Match(acfContent, "\"installdir\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    internal EpicManifestInfo? TryFindEpicManifest(EditionSpec edition, string? gameDirectory)
    {
        if (!Directory.Exists(_epicManifestsDirectory))
        {
            return null;
        }

        var pendingDir = Path.Combine(_epicManifestsDirectory, "Pending");
        var itemFiles = Directory.EnumerateFiles(_epicManifestsDirectory, "*.item", SearchOption.TopDirectoryOnly);
        if (Directory.Exists(pendingDir))
        {
            itemFiles = itemFiles.Concat(Directory.EnumerateFiles(pendingDir, "*.item", SearchOption.TopDirectoryOnly));
        }

        foreach (var itemFile in itemFiles)
        {
            try
            {
                var info = ParseEpicManifest(File.ReadAllText(itemFile));
                if (info is null)
                {
                    continue;
                }

                // Exact path match takes precedence when gameDirectory is known.
                if (!string.IsNullOrWhiteSpace(gameDirectory) &&
                    string.Equals(
                        Path.GetFullPath(info.InstallLocation).TrimEnd('\\', '/'),
                        Path.GetFullPath(gameDirectory).TrimEnd('\\', '/'),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return info;
                }

                if (!string.IsNullOrWhiteSpace(edition.DefaultEpicAppId) &&
                    string.Equals(info.AppName, edition.DefaultEpicAppId, StringComparison.OrdinalIgnoreCase))
                {
                    return info;
                }

                var isEnhancedName = info.DisplayName.Contains("Grand Theft Auto V Enhanced", StringComparison.OrdinalIgnoreCase)
                    || info.DisplayName.Contains("GTA V Enhanced", StringComparison.OrdinalIgnoreCase);
                var isLegacyName = !isEnhancedName &&
                    info.DisplayName.Contains("Grand Theft Auto V", StringComparison.OrdinalIgnoreCase);

                if (edition.Edition == GameEdition.Enhanced && isEnhancedName)
                {
                    return info;
                }

                if (edition.Edition == GameEdition.Legacy && isLegacyName)
                {
                    return info;
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"Failed to parse Epic manifest {itemFile}: {ex.Message}");
            }
        }

        return null;
    }

    internal static EpicManifestInfo? ParseEpicManifest(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var appName = root.TryGetProperty("AppName", out var appProp) ? appProp.GetString() : null;
        var displayName = root.TryGetProperty("DisplayName", out var dispProp) ? dispProp.GetString() : null;
        var installLocation = root.TryGetProperty("InstallLocation", out var locProp) ? locProp.GetString() : null;

        if (string.IsNullOrWhiteSpace(appName) || string.IsNullOrWhiteSpace(installLocation))
        {
            return null;
        }

        return new EpicManifestInfo(appName.Trim(), (displayName ?? string.Empty).Trim(), installLocation.Trim());
    }
}
