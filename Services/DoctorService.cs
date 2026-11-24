using System.Reflection;
using System.Text;
using Grand_Theft_Auto_V_Platform_Switcher.Models;

namespace Grand_Theft_Auto_V_Platform_Switcher.Services;

public sealed class DoctorService
{
    private readonly EditionDetector _detector;
    private readonly PlatformSwitchService _switchService;
    private readonly BattlEyeService _battlEyeService;
    private readonly RegistryService _registryService;
    private readonly ProcessGuard _processGuard;
    private readonly EpicManifestService _epicManifestService;

    public DoctorService(
        EditionDetector detector,
        PlatformSwitchService switchService,
        BattlEyeService battlEyeService,
        RegistryService registryService,
        ProcessGuard processGuard,
        EpicManifestService? epicManifestService = null)
    {
        _detector = detector;
        _switchService = switchService;
        _battlEyeService = battlEyeService;
        _registryService = registryService;
        _processGuard = processGuard;
        _epicManifestService = epicManifestService ?? new EpicManifestService(processGuard, new AppLogger(), detector.EpicManifestsDirectory);
    }

    public DoctorReport Diagnose(EditionSpec edition, EditionSettings editionSettings)
    {
        var checks = new List<DoctorCheckItem>();
        var gameDir = editionSettings.GameDirectory;
        var isElevated = _registryService.IsAdministrator();
        var isValidDir = _detector.IsValidGameDirectory(edition, gameDir);
        var detectedPlatform = isValidDir ? _detector.DetectActivePlatform(edition, gameDir) : GamePlatform.Unknown;
        var effectivePlatform = editionSettings.CurrentPlatform != GamePlatform.Unknown
            ? editionSettings.CurrentPlatform
            : detectedPlatform;

        // 1. Game Directory & Write Access
        if (!isValidDir)
        {
            var auto = _detector.DetectInstallation(edition);
            checks.Add(new DoctorCheckItem
            {
                Id = "game-directory",
                Title = "Game Folder",
                Severity = DoctorSeverity.Error,
                Summary = $"GTA V ({edition.DisplayName}) was not found in the selected folder.",
                Details = auto.GameDirectory is not null
                    ? $"Found automatically via {auto.Source}: {auto.GameDirectory}"
                    : $"Click Choose Folder and select the folder containing {edition.MainExecutable}.",
                FixType = auto.GameDirectory is not null
                    ? DoctorFixType.AutoDetectGamePath
                    : DoctorFixType.BrowseGamePath,
                FixLabel = auto.GameDirectory is not null ? "Use Found Folder" : "Choose Folder..."
            });
        }
        else if (!_switchService.CanWriteToDirectory(gameDir))
        {
            checks.Add(new DoctorCheckItem
            {
                Id = "game-directory",
                Title = "Folder Permissions",
                Severity = DoctorSeverity.Error,
                Summary = "Windows is blocking edits to this folder (common inside Program Files).",
                Details = $"Folder '{gameDir}' requires Administrator permission to update files.",
                FixType = DoctorFixType.RestartAsAdmin,
                FixLabel = "Restart as Administrator"
            });
        }
        else
        {
            checks.Add(new DoctorCheckItem
            {
                Id = "game-directory",
                Title = "Game Folder & Permissions",
                Severity = DoctorSeverity.Ok,
                Summary = $"Valid {edition.DisplayName} folder with write permission.",
                Details = gameDir
            });
        }

        // 2. Running Process Check
        var blocking = _processGuard.GetBlockingProcesses(edition);
        checks.Add(blocking.Count > 0
            ? new DoctorCheckItem
            {
                Id = "process-lock",
                Title = "Game Running Check",
                Severity = DoctorSeverity.Warning,
                Summary = $"GTA V is currently running ({string.Join(", ", blocking)}).",
                Details = "Close the game completely before switching launchers to avoid locked file errors."
            }
            : new DoctorCheckItem
            {
                Id = "process-lock",
                Title = "Game Running Check",
                Severity = DoctorSeverity.Ok,
                Summary = "GTA V is not running right now."
            });

        if (isValidDir)
        {
            // 3. Switcher Profiles & Version Sync
            var versions = _switchService.GetVersionSnapshot(edition, gameDir);
            var populatedCount = (versions.HasRglProfile ? 1 : 0) +
                                 (versions.HasSteamProfile ? 1 : 0) +
                                 (versions.HasEglProfile ? 1 : 0);
            var outdatedList = new List<string>();
            if (versions.IsRglOutdated) outdatedList.Add("Rockstar Games");
            if (versions.IsSteamOutdated) outdatedList.Add("Steam");
            if (versions.IsEglOutdated) outdatedList.Add("Epic Games");

            if (populatedCount == 0)
            {
                var targetSlot = effectivePlatform != GamePlatform.Unknown ? effectivePlatform : GamePlatform.RGL;
                checks.Add(new DoctorCheckItem
                {
                    Id = "switcher-profiles",
                    Title = "Saved Launchers",
                    Severity = DoctorSeverity.Warning,
                    Summary = "No launcher files have been saved yet.",
                    Details = $"Your folder currently looks like {GetPlatformFriendlyName(effectivePlatform)}. Save its launcher files first so you can switch back later.",
                    FixType = DoctorFixType.BackupActivePlatform,
                    FixLabel = $"Save {GetPlatformShortName(targetSlot)} Files",
                    SuggestedPlatform = targetSlot
                });
            }
            else if (outdatedList.Count > 0)
            {
                var gameVerNote = FormatGameVersionTag(versions.ActiveGameVersion);
                checks.Add(new DoctorCheckItem
                {
                    Id = "switcher-profiles",
                    Title = "Saved Launchers",
                    Severity = DoctorSeverity.Warning,
                    Summary = $"Older launcher files found: {string.Join(", ", outdatedList)} ({gameVerNote}).",
                    Details = "Your game updated recently. Refresh saved files before switching so you don't roll back to an older version.",
                    FixType = effectivePlatform != GamePlatform.Unknown
                        ? DoctorFixType.BackupActivePlatform
                        : DoctorFixType.None,
                    FixLabel = effectivePlatform != GamePlatform.Unknown ? $"Refresh {GetPlatformShortName(effectivePlatform)}" : string.Empty,
                    SuggestedPlatform = effectivePlatform
                });
            }
            else
            {
                var gameVerNote = FormatGameVersionTag(versions.ActiveGameVersion);
                checks.Add(new DoctorCheckItem
                {
                    Id = "switcher-profiles",
                    Title = "Saved Launchers",
                    Severity = DoctorSeverity.Ok,
                    Summary = $"{populatedCount}/3 launchers saved and up to date ({gameVerNote}).",
                    Details = $"Rockstar: {versions.RglProfileVersion} | Steam: {versions.SteamProfileVersion} | Epic: {versions.EglProfileVersion}"
                });
            }

            if (versions.IsEglProfileInvalid)
            {
                checks.Add(new DoctorCheckItem
                {
                    Id = "invalid-epic-profile",
                    Title = "Epic Games Backup Integrity",
                    Severity = DoctorSeverity.Warning,
                    Summary = "Saved Epic Games backup is missing required Epic files (EOSSDK-Win64-Shipping.dll).",
                    Details = "Clean this invalid backup before linking or setting up Epic Games.",
                    FixType = DoctorFixType.CleanInvalidProfile,
                    FixLabel = "Clean Invalid Backup",
                    SuggestedPlatform = GamePlatform.EGL
                });
            }

            var epicInspection = _epicManifestService.Inspect(edition, gameDir, editionSettings.EpicAppIdOverride);
            if (epicInspection.State == EpicManifestState.PointsElsewhere)
            {
                var summary = epicInspection.HasOrphanDuplicates &&
                              string.Equals(
                                  epicInspection.CurrentInstallLocation?.TrimEnd('\\', '/'),
                                  gameDir.TrimEnd('\\', '/'),
                                  StringComparison.OrdinalIgnoreCase)
                    ? "Duplicate pending Epic Games manifest entries need cleanup."
                    : "Epic Games Launcher points to a different folder for this game.";

                checks.Add(new DoctorCheckItem
                {
                    Id = "epic-manifest-sync",
                    Title = "Epic Games Link",
                    Severity = DoctorSeverity.Warning,
                    Summary = summary,
                    Details = $"Current Epic folder: '{epicInspection.CurrentInstallLocation ?? "-"}'. Click Link Epic Folder to sync Epic Games Launcher and clean pending metadata for '{gameDir}'.",
                    FixType = DoctorFixType.AdoptEpicInstallation,
                    FixLabel = "Link Epic Folder",
                    SuggestedPlatform = GamePlatform.EGL
                });
            }
            else if (epicInspection.State == EpicManifestState.AlignedMissingEgstore && epicInspection.HasSourceEgstore)
            {
                checks.Add(new DoctorCheckItem
                {
                    Id = "epic-manifest-sync",
                    Title = "Epic Games Link",
                    Severity = DoctorSeverity.Warning,
                    Summary = "Epic manifest points here, but the '.egstore' folder is missing.",
                    Details = "Click Link Epic Folder to restore '.egstore' metadata and refresh your Epic Games Launcher manifest.",
                    FixType = DoctorFixType.AdoptEpicInstallation,
                    FixLabel = "Link Epic Folder",
                    SuggestedPlatform = GamePlatform.EGL
                });
            }
            else if (epicInspection.State == EpicManifestState.Aligned)
            {
                var awaitingVerify = !versions.HasEglProfile &&
                                     !PlatformSwitchService.HasActiveEpicSdk(gameDir) &&
                                     !PlatformSwitchService.HasParkedEpicSdk(gameDir);
                checks.Add(new DoctorCheckItem
                {
                    Id = "epic-manifest-sync",
                    Title = "Epic Games Link",
                    Severity = DoctorSeverity.Ok,
                    Summary = awaitingVerify
                        ? "Epic Games folder is linked — Resume / Verify in Epic Launcher to finish."
                        : "Epic Games Launcher manifest matches this game folder."
                });
            }
            else if (effectivePlatform == GamePlatform.EGL)
            {
                checks.Add(new DoctorCheckItem
                {
                    Id = "epic-manifest-sync",
                    Title = "Epic Games Link",
                    Severity = DoctorSeverity.Warning,
                    Summary = "Epic Games Launcher is not linked to this game folder yet.",
                    Details = "Start installing GTA V in Epic Games Launcher to any empty temp folder for 5-10 seconds, pause it, close Epic Games Launcher, and click Set Up Epic.",
                    FixType = DoctorFixType.AdoptEpicInstallation,
                    FixLabel = "Set Up Epic",
                    SuggestedPlatform = GamePlatform.EGL
                });
            }
            else
            {
                checks.Add(new DoctorCheckItem
                {
                    Id = "epic-manifest-sync",
                    Title = "Epic Games Link",
                    Severity = DoctorSeverity.Ok,
                    Summary = "Optional (not configured)",
                    Details = "Only needed if you also own GTA V on Epic Games.",
                    FixType = DoctorFixType.AdoptEpicInstallation,
                    FixLabel = "Set Up...",
                    SuggestedPlatform = GamePlatform.EGL
                });
            }

            // 4. steam_api64.dll & EOSSDK Conflict Checks
            var hasSteamOrig = File.Exists(Path.Combine(gameDir, "steam_api64.dll"));
            var hasSteamBack = File.Exists(Path.Combine(gameDir, "steam_api64.dll.steam"));

            if ((hasSteamOrig && hasSteamBack) ||
                (effectivePlatform == GamePlatform.Steam && !hasSteamOrig && hasSteamBack) ||
                (effectivePlatform != GamePlatform.Steam && effectivePlatform != GamePlatform.Unknown && hasSteamOrig))
            {
                checks.Add(new DoctorCheckItem
                {
                    Id = "steam-api-dll",
                    Title = "Steam Launcher Files",
                    Severity = DoctorSeverity.Warning,
                    Summary = "Leftover Steam launcher files conflict with your active launcher.",
                    Details = $"File check: steam_api64.dll={hasSteamOrig}, backup={hasSteamBack}, active={GetPlatformFriendlyName(effectivePlatform)}.",
                    FixType = DoctorFixType.RepairSteamApiConflict,
                    FixLabel = "Fix Steam Files",
                    SuggestedPlatform = effectivePlatform
                });
            }
            else
            {
                checks.Add(new DoctorCheckItem
                {
                    Id = "steam-api-dll",
                    Title = "Steam Launcher Files",
                    Severity = DoctorSeverity.Ok,
                    Summary = "Steam launcher files look clean."
                });
            }

            var hasEosOrig = PlatformSwitchService.HasActiveEpicSdk(gameDir);
            var hasEosBack = PlatformSwitchService.HasParkedEpicSdk(gameDir);
            if ((hasEosOrig && hasEosBack) ||
                (effectivePlatform == GamePlatform.EGL && !hasEosOrig && hasEosBack) ||
                (effectivePlatform != GamePlatform.EGL && effectivePlatform != GamePlatform.Unknown && hasEosOrig))
            {
                checks.Add(new DoctorCheckItem
                {
                    Id = "epic-eos-dll",
                    Title = "Epic Games Launcher Files",
                    Severity = DoctorSeverity.Warning,
                    Summary = "Leftover Epic Games files conflict with your active launcher.",
                    Details = $"File check: activeEpicSdk={hasEosOrig}, backup={hasEosBack}, active={GetPlatformFriendlyName(effectivePlatform)}.",
                    FixType = DoctorFixType.RepairEpicDllConflict,
                    FixLabel = "Fix Epic Files",
                    SuggestedPlatform = effectivePlatform
                });
            }

            // 5. BattlEye Health
            var beStatus = _battlEyeService.Inspect(edition, gameDir);
            checks.Add(new DoctorCheckItem
            {
                Id = "battleye-health",
                Title = "BattlEye Anti-Cheat",
                Severity = beStatus.IsHealthy ? DoctorSeverity.Ok : DoctorSeverity.Warning,
                Summary = beStatus.Summary,
                Details = beStatus.IsHealthy
                    ? "BattlEye is ready for GTA Online. To use Story Mode mods, check \"Disable BattlEye\" before launching (you may also need to turn off BattlEye inside Rockstar Games Launcher settings)."
                    : "Verify game integrity via Steam, Epic Games Launcher, or Rockstar Games Launcher to restore missing BattlEye files."
            });

            // 6. Registry Consistency & Elevation
            var reg = _registryService.ReadStatus(edition);
            var normalizedDir = Path.GetFullPath(gameDir).TrimEnd('\\', '/');
            var regMismatch = false;
            var regNote = $"SteamInstalled={reg.SteamInstalledFlag}, InstallFolder='{reg.InstallFolderRgl ?? "-"}', InstallFolderSteam='{reg.InstallFolderSteam ?? "-"}', InstallFolderEpic='{reg.InstallFolderEpic ?? "-"}'";

            if (effectivePlatform == GamePlatform.Steam &&
                (!reg.SteamInstalledFlag ||
                 !string.Equals(reg.InstallFolderSteam?.TrimEnd('\\', '/'), normalizedDir, StringComparison.OrdinalIgnoreCase)))
            {
                regMismatch = true;
            }
            else if (effectivePlatform == GamePlatform.EGL &&
                     !string.Equals(reg.InstallFolderEpic?.TrimEnd('\\', '/'), normalizedDir, StringComparison.OrdinalIgnoreCase))
            {
                regMismatch = true;
            }
            else if (effectivePlatform == GamePlatform.RGL &&
                     (reg.SteamInstalledFlag ||
                      !string.Equals(reg.InstallFolderRgl?.TrimEnd('\\', '/'), normalizedDir, StringComparison.OrdinalIgnoreCase)))
            {
                regMismatch = true;
            }

            if (regMismatch)
            {
                checks.Add(new DoctorCheckItem
                {
                    Id = "registry-sync",
                    Title = "Launcher Registry Settings",
                    Severity = DoctorSeverity.Warning,
                    Summary = $"Windows launcher settings do not match your active launcher ({GetPlatformFriendlyName(effectivePlatform)}).",
                    Details = regNote,
                    FixType = isElevated
                        ? DoctorFixType.SyncRegistryForActivePlatform
                        : DoctorFixType.RestartAsAdmin,
                    FixLabel = isElevated ? "Fix Launcher Settings" : "Restart as Administrator",
                    SuggestedPlatform = effectivePlatform
                });
            }
            else
            {
                checks.Add(new DoctorCheckItem
                {
                    Id = "registry-sync",
                    Title = "Launcher Registry Settings",
                    Severity = DoctorSeverity.Ok,
                    Summary = "Windows launcher settings match your active launcher.",
                    Details = regNote
                });
            }
        }

        return new DoctorReport
        {
            Edition = edition.Edition,
            GameDirectory = gameDir,
            ConfiguredPlatform = editionSettings.CurrentPlatform,
            DetectedActivePlatform = detectedPlatform,
            IsElevated = isElevated,
            GeneratedAt = DateTimeOffset.Now,
            Checks = checks
        };
    }

    public async Task<OperationResult> ApplyFixAsync(
        DoctorCheckItem item,
        EditionSpec edition,
        EditionSettings editionSettings)
    {
        switch (item.FixType)
        {
            case DoctorFixType.AutoDetectGamePath:
            {
                var result = _detector.DetectInstallation(edition);
                if (result.GameDirectory is null)
                {
                    return new OperationResult(false, "Could not auto-detect game directory.");
                }

                editionSettings.GameDirectory = result.GameDirectory;
                if (editionSettings.CurrentPlatform == GamePlatform.Unknown)
                {
                    editionSettings.CurrentPlatform = _detector.DetectActivePlatform(edition, result.GameDirectory);
                }

                return new OperationResult(true, $"Set {edition.DisplayName} path to '{result.GameDirectory}' ({result.Source}).");
            }

            case DoctorFixType.BackupActivePlatform:
            {
                var platform = item.SuggestedPlatform != GamePlatform.Unknown
                    ? item.SuggestedPlatform
                    : GamePlatform.RGL;
                var backupRes = await _switchService.BackupCurrentPlatformAsync(
                    edition,
                    editionSettings.GameDirectory,
                    platform).ConfigureAwait(false);
                if (backupRes.Success && editionSettings.CurrentPlatform == GamePlatform.Unknown)
                {
                    editionSettings.CurrentPlatform = platform;
                }

                return backupRes;
            }

            case DoctorFixType.RepairSteamApiConflict:
                return _switchService.RepairSteamApiConflict(editionSettings.GameDirectory, item.SuggestedPlatform);

            case DoctorFixType.RepairEpicDllConflict:
                return _switchService.RepairEpicDllConflict(editionSettings.GameDirectory, item.SuggestedPlatform);

            case DoctorFixType.SyncRegistryForActivePlatform:
            {
                try
                {
                    if (item.SuggestedPlatform == GamePlatform.Steam)
                    {
                        _registryService.AddPlayOnSteam(edition, editionSettings.GameDirectory);
                    }
                    else if (item.SuggestedPlatform == GamePlatform.EGL)
                    {
                        _registryService.AddPlayOnEpic(edition, editionSettings.GameDirectory);
                    }
                    else if (item.SuggestedPlatform == GamePlatform.RGL)
                    {
                        _registryService.SyncPlayOnRgl(edition, editionSettings.GameDirectory);
                    }

                    return new OperationResult(true, $"Updated Windows launcher settings for {edition.DisplayName} ({GetPlatformFriendlyName(item.SuggestedPlatform)}).");
                }
                catch (Exception ex)
                {
                    return new OperationResult(false, ex.Message, ex is UnauthorizedAccessException);
                }
            }

            case DoctorFixType.RestartAsAdmin:
            {
                var restarted = _registryService.RestartAsAdministrator();
                return restarted
                    ? new OperationResult(true, "Relaunching as Administrator...")
                    : new OperationResult(false, "Elevation was cancelled.");
            }

            case DoctorFixType.CleanInvalidProfile:
            {
                var target = item.SuggestedPlatform != GamePlatform.Unknown
                    ? item.SuggestedPlatform
                    : GamePlatform.EGL;
                return _switchService.CleanInvalidProfile(edition, editionSettings.GameDirectory, target);
            }

            case DoctorFixType.AdoptEpicInstallation:
            {
                return await _epicManifestService.RelocateAndAdoptAsync(
                    edition,
                    editionSettings.GameDirectory,
                    _switchService,
                    editionSettings.EpicAppIdOverride).ConfigureAwait(false);
            }

            default:
                return new OperationResult(false, "This item requires manual action.");
        }
    }

    public Task<OperationResult> AdoptEpicFromFolderAsync(
        EditionSpec edition,
        EditionSettings editionSettings,
        string sourceEpicFolder) =>
        _epicManifestService.RelocateAndAdoptFromFolderAsync(
            edition,
            editionSettings.GameDirectory,
            sourceEpicFolder,
            _switchService,
            editionSettings.EpicAppIdOverride);

    public EpicManifestInspection InspectEpicManifest(EditionSpec edition, EditionSettings editionSettings) =>
        _epicManifestService.Inspect(edition, editionSettings.GameDirectory, editionSettings.EpicAppIdOverride);

    public string BuildMarkdownReport(
        DoctorReport report,
        VersionSnapshot versions,
        BattlEyeStatus battlEye)
    {
        var appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2.0.0";
        var sb = new StringBuilder();
        sb.AppendLine("## GTA V Platform Switcher - Diagnostic Report");
        sb.AppendLine();
        sb.AppendLine($"- **App Version**: `v{appVersion}`");
        sb.AppendLine($"- **OS**: `{Environment.OSVersion}` (64-bit: `{Environment.Is64BitOperatingSystem}`)");
        sb.AppendLine($"- **Elevated (Admin)**: `{report.IsElevated}`");
        sb.AppendLine($"- **Edition**: `{report.Edition}`");
        sb.AppendLine($"- **Configured Platform**: `{report.ConfiguredPlatform}` (Heuristic: `{report.DetectedActivePlatform}`)");
        sb.AppendLine($"- **Active Game Version**: `{versions.ActiveGameVersion}`");
        sb.AppendLine($"- **Profiles**: RGL=`{versions.RglProfileVersion}` | Steam=`{versions.SteamProfileVersion}` | Epic=`{versions.EglProfileVersion}`");
        sb.AppendLine($"- **BattlEye**: `{battlEye.Summary}`");
        sb.AppendLine($"- **Generated**: `{report.GeneratedAt:yyyy-MM-dd HH:mm:ss zzz}`");
        sb.AppendLine();
        sb.AppendLine("### Checks");
        foreach (var check in report.Checks)
        {
            var tag = check.Severity switch
            {
                DoctorSeverity.Ok => "OK",
                DoctorSeverity.Warning => "WARN",
                DoctorSeverity.Error => "ERROR",
                _ => "INFO"
            };
            sb.AppendLine($"- **[{tag}] {check.Title}**: {check.Summary}");
            if (!string.IsNullOrWhiteSpace(check.Details))
            {
                sb.AppendLine($"  - {check.Details}");
            }
        }

        return sb.ToString();
    }

    private static string FormatGameVersionTag(string version)
    {
        if (string.IsNullOrWhiteSpace(version) ||
            version.Equals("Present", StringComparison.OrdinalIgnoreCase) ||
            version.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return "matches current game";
        }

        return char.IsDigit(version[0]) ? $"v{version}" : version;
    }

    private static string GetPlatformFriendlyName(GamePlatform platform) =>
        platform == GamePlatform.Unknown ? "Unknown" : EditionSpec.GetPlatformDisplayName(platform);

    private static string GetPlatformShortName(GamePlatform platform) => platform switch
    {
        GamePlatform.RGL => "Rockstar",
        GamePlatform.Steam => "Steam",
        GamePlatform.EGL => "Epic",
        _ => "Rockstar"
    };
}
