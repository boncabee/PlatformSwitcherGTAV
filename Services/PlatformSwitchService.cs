using System.Diagnostics;
using Grand_Theft_Auto_V_Platform_Switcher.Models;

namespace Grand_Theft_Auto_V_Platform_Switcher.Services;

public sealed record VersionSnapshot(
    string ActiveGameVersion,
    string RglProfileVersion,
    string SteamProfileVersion,
    string EglProfileVersion,
    bool HasRglProfile,
    bool HasSteamProfile,
    bool HasEglProfile,
    bool IsRglOutdated,
    bool IsSteamOutdated,
    bool IsEglOutdated,
    bool IsEglProfileInvalid = false);

public sealed record VersionMismatchInfo(
    bool IsOutdated,
    string ActiveVersion,
    string ProfileVersion);

public sealed record OperationResult(bool Success, string Message, bool RequiresElevation = false);

public sealed class PlatformSwitchService
{
    private static readonly string[] ManagedRuntimeDlls =
    [
        "steam_api64.dll",
        "steam_api64.dll.steam",
        "EOSSDK-Win64-Shipping.dll",
        "EOSSDK-Win64-Shipping.dll.epic"
    ];

    private sealed record DllRollbackEntry(string TargetPath, bool ExistedBefore, string? BackupPath);

    private readonly EditionDetector _detector;
    private readonly ProcessGuard _processGuard;
    private readonly AppLogger _logger;

    public PlatformSwitchService(EditionDetector detector, ProcessGuard processGuard, AppLogger logger)
    {
        _detector = detector;
        _processGuard = processGuard;
        _logger = logger;
    }

    public bool CanWriteToDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        var probeFile = Path.Combine(directory, $".switcher_write_test_{Guid.NewGuid():N}.tmp");
        try
        {
            using (File.Create(probeFile)) { }
            File.Delete(probeFile);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public VersionSnapshot GetVersionSnapshot(EditionSpec edition, string? gameDirectory)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || !Directory.Exists(gameDirectory))
        {
            return new VersionSnapshot("Unknown", "Not set", "Not set", "Not set", false, false, false, false, false, false);
        }

        var activeVer = ReadExecutableVersion(Path.Combine(gameDirectory, edition.MainExecutable));

        var rglDir = _detector.ResolveProfileDirectory(edition, gameDirectory, GamePlatform.RGL);
        var steamDir = _detector.ResolveProfileDirectory(edition, gameDirectory, GamePlatform.Steam);
        var eglDir = _detector.ResolveProfileDirectory(edition, gameDirectory, GamePlatform.EGL);

        var rglExe = Path.Combine(rglDir, edition.MainExecutable);
        var steamExe = Path.Combine(steamDir, edition.MainExecutable);
        var eglExe = Path.Combine(eglDir, edition.MainExecutable);

        var hasRgl = File.Exists(rglExe);
        var hasSteam = File.Exists(steamExe);
        var hasEglExe = File.Exists(eglExe);
        var hasValidEglSdk = HasValidEpicSdkForProfile(eglDir, gameDirectory);
        var isEglInvalid = hasEglExe && !hasValidEglSdk;
        var hasEgl = hasEglExe && hasValidEglSdk;

        var rglVer = hasRgl ? ReadExecutableVersion(rglExe) : "Not set";
        var steamVer = hasSteam ? ReadExecutableVersion(steamExe) : "Not set";
        var eglVer = hasEgl
            ? ReadExecutableVersion(eglExe)
            : isEglInvalid
                ? "Invalid"
                : "Not set";

        return new VersionSnapshot(
            activeVer,
            rglVer,
            steamVer,
            eglVer,
            hasRgl,
            hasSteam,
            hasEgl,
            hasRgl && IsProfileOlderThanActive(activeVer, rglVer),
            hasSteam && IsProfileOlderThanActive(activeVer, steamVer),
            hasEgl && IsProfileOlderThanActive(activeVer, eglVer),
            IsEglProfileInvalid: isEglInvalid);
    }

    public static IReadOnlyList<string> GetActiveEpicSdkFiles(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(directory, "EOSSDK-Win64-Shipping*.dll", SearchOption.TopDirectoryOnly)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<string> GetParkedEpicSdkFiles(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(directory, "EOSSDK-Win64-Shipping*.dll.epic", SearchOption.TopDirectoryOnly)
            .Where(path => path.EndsWith(".dll.epic", StringComparison.OrdinalIgnoreCase))
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool HasActiveEpicSdk(string? directory) => GetActiveEpicSdkFiles(directory).Count > 0;

    public static bool HasParkedEpicSdk(string? directory) => GetParkedEpicSdkFiles(directory).Count > 0;

    public static bool HasValidEpicSdkForProfile(string eglProfileDir, string gameDirectory) =>
        HasActiveEpicSdk(eglProfileDir) || HasParkedEpicSdk(gameDirectory);

    public VersionMismatchInfo CheckProfileVersionMismatch(
        EditionSpec edition,
        string gameDirectory,
        GamePlatform targetPlatform)
    {
        var activeVer = ReadExecutableVersion(Path.Combine(gameDirectory, edition.MainExecutable));
        var profileDir = _detector.ResolveProfileDirectory(edition, gameDirectory, targetPlatform);
        var profileVer = ReadExecutableVersion(Path.Combine(profileDir, edition.MainExecutable));
        var isOutdated = IsProfileOlderThanActive(activeVer, profileVer);
        return new VersionMismatchInfo(isOutdated, activeVer, profileVer);
    }

    internal static bool IsProfileOlderThanActive(string activeVersion, string profileVersion)
    {
        if (!TryParseComparableVersion(activeVersion, out var active) ||
            !TryParseComparableVersion(profileVersion, out var profile))
        {
            return false;
        }

        return profile < active;
    }

    private static bool TryParseComparableVersion(string? raw, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var cleaned = raw.Trim().Replace(',', '.').Split(' ')[0];
        return Version.TryParse(cleaned, out version!);
    }

    public async Task<OperationResult> SwitchAsync(
        EditionSpec edition,
        string gameDirectory,
        GamePlatform targetPlatform,
        IProgress<string>? progress = null,
        Action<string>? testFailAfterCopyFile = null,
        CancellationToken cancellationToken = default,
        Action? testFailAfterSteamDllUpdate = null)
    {
        if (targetPlatform == GamePlatform.Unknown)
        {
            return new OperationResult(false, "Please select a valid target platform (Rockstar Games, Steam, or Epic Games).");
        }

        if (!_detector.IsValidGameDirectory(edition, gameDirectory))
        {
            return new OperationResult(false, $"Could not find {edition.MainExecutable} in '{gameDirectory}'.");
        }

        var blocking = _processGuard.GetBlockingProcesses(edition);
        if (blocking.Count > 0)
        {
            return new OperationResult(
                false,
                $"Cannot switch while the game is running: {string.Join(", ", blocking)}. Please close the game first.");
        }

        if (!CanWriteToDirectory(gameDirectory))
        {
            return new OperationResult(
                false,
                $"No write permission to '{gameDirectory}'. Run the application as Administrator.",
                RequiresElevation: true);
        }

        var sourceProfileDir = _detector.ResolveProfileDirectory(edition, gameDirectory, targetPlatform);
        if (!Directory.Exists(sourceProfileDir) || !Directory.EnumerateFiles(sourceProfileDir, "*", SearchOption.AllDirectories).Any())
        {
            return new OperationResult(
                false,
                $"Launcher files for {EditionSpec.GetPlatformDisplayName(targetPlatform)} are not set up yet. Set up this launcher or refresh saved files first.");
        }

        if (targetPlatform == GamePlatform.EGL && !HasValidEpicSdkForProfile(sourceProfileDir, gameDirectory))
        {
            return new OperationResult(
                false,
                "The saved Epic Games backup is incomplete (missing EOSSDK-Win64-Shipping.dll) and appears to have been created from another launcher. Clean or re-create your Epic Games backup first.");
        }

        var rollbackDir = Path.Combine(Path.GetTempPath(), $"PlatformSwitcherGTAV_Rollback_{Guid.NewGuid():N}");
        var copiedNewFiles = new List<string>();
        var backedUpTargets = new List<(string OriginalPath, string BackupPath)>();
        var dllSnapshots = new List<DllRollbackEntry>();

        try
        {
            Directory.CreateDirectory(rollbackDir);
            var dllStageDir = Path.Combine(rollbackDir, "__dll_state");
            Directory.CreateDirectory(dllStageDir);
            var managedDllNames = new HashSet<string>(ManagedRuntimeDlls, StringComparer.OrdinalIgnoreCase);
            foreach (var eosPath in GetActiveEpicSdkFiles(gameDirectory).Concat(GetActiveEpicSdkFiles(sourceProfileDir)))
            {
                var fileName = Path.GetFileName(eosPath);
                managedDllNames.Add(fileName);
                managedDllNames.Add(fileName + ".epic");
            }
            foreach (var parkedPath in GetParkedEpicSdkFiles(gameDirectory))
            {
                var parkedName = Path.GetFileName(parkedPath);
                managedDllNames.Add(parkedName);
                managedDllNames.Add(parkedName[..^5]);
            }

            foreach (var dllName in managedDllNames)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var targetPath = Path.Combine(gameDirectory, dllName);
                if (File.Exists(targetPath))
                {
                    var stagePath = Path.Combine(dllStageDir, dllName);
                    File.Copy(targetPath, stagePath, overwrite: true);
                    dllSnapshots.Add(new DllRollbackEntry(targetPath, ExistedBefore: true, stagePath));
                }
                else
                {
                    dllSnapshots.Add(new DllRollbackEntry(targetPath, ExistedBefore: false, BackupPath: null));
                }
            }

            var sourceFiles = Directory.GetFiles(sourceProfileDir, "*", SearchOption.AllDirectories);

            // Stage existing destination files before overwriting so rollback is guaranteed.
            progress?.Report($"Staging rollback backup ({sourceFiles.Length} file(s))...");
            foreach (var srcFile in sourceFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(sourceProfileDir, srcFile);
                var destFile = Path.Combine(gameDirectory, relative);
                if (File.Exists(destFile))
                {
                    var stagePath = Path.Combine(rollbackDir, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(stagePath)!);
                    File.Copy(destFile, stagePath, overwrite: true);
                    backedUpTargets.Add((destFile, stagePath));
                }
            }

            // Copy profile files asynchronously.
            for (var i = 0; i < sourceFiles.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var srcFile = sourceFiles[i];
                var relative = Path.GetRelativePath(sourceProfileDir, srcFile);
                var destFile = Path.Combine(gameDirectory, relative);
                var existedBefore = File.Exists(destFile);

                progress?.Report($"Copying {relative} ({i + 1}/{sourceFiles.Length})...");
                Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
                await CopyFileAsync(srcFile, destFile, cancellationToken).ConfigureAwait(false);

                if (!existedBefore)
                {
                    copiedNewFiles.Add(destFile);
                }

                testFailAfterCopyFile?.Invoke(relative);
            }

            // Handle steam_api64.dll and EOSSDK-Win64-Shipping.dll cleanly.
            progress?.Report("Updating launcher files...");
            UpdateSteamApiDll(gameDirectory, targetPlatform);
            testFailAfterSteamDllUpdate?.Invoke();
            UpdateEpicSdkDll(gameDirectory, targetPlatform, sourceProfileDir);

            _logger.Info($"Switched {edition.DisplayName} in '{gameDirectory}' to {targetPlatform} ({sourceFiles.Length} files copied).");
            progress?.Report($"Switched to {EditionSpec.GetPlatformDisplayName(targetPlatform)}.");
            return new OperationResult(true, $"Successfully switched {edition.DisplayName} to {EditionSpec.GetPlatformDisplayName(targetPlatform)}.");
        }
        catch (Exception ex)
        {
            _logger.Error($"Switch to {targetPlatform} failed. Rolling back changes...", ex);
            Rollback(backedUpTargets, copiedNewFiles, dllSnapshots);

            var requiresAdmin = ex is UnauthorizedAccessException;
            return new OperationResult(
                false,
                $"Switch failed and changes were rolled back: {ex.Message}",
                RequiresElevation: requiresAdmin);
        }
        finally
        {
            TryDeleteDirectory(rollbackDir);
        }
    }

    public async Task<OperationResult> BackupCurrentPlatformAsync(
        EditionSpec edition,
        string gameDirectory,
        GamePlatform platform,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default,
        Action<string>? testFailAfterCopyFile = null)
    {
        if (platform == GamePlatform.Unknown)
        {
            return new OperationResult(false, "Select Rockstar Games, Steam, or Epic Games as the backup target.");
        }

        if (!_detector.IsValidGameDirectory(edition, gameDirectory))
        {
            return new OperationResult(false, $"Could not find {edition.MainExecutable} in '{gameDirectory}'.");
        }

        var signatureValidation = ValidateActiveFolderForBackup(gameDirectory, platform);
        if (!signatureValidation.Success)
        {
            return signatureValidation;
        }

        var targetDir = _detector.GetPrimaryProfileDirectory(edition, gameDirectory, platform);
        var backupRoot = Path.GetDirectoryName(targetDir)!;
        Directory.CreateDirectory(backupRoot);
        if (!CanWriteToDirectory(backupRoot))
        {
            return new OperationResult(
                false,
                $"No write permission to '{backupRoot}'. Move the app to a folder you can write to, or run as Administrator.",
                RequiresElevation: true);
        }

        var parentDir = Path.GetDirectoryName(targetDir)!;
        Directory.CreateDirectory(parentDir);

        var targetName = Path.GetFileName(targetDir);
        var stagingDir = Path.Combine(parentDir, $".{targetName}_staging_{Guid.NewGuid():N}");
        string? previousDir = null;

        try
        {
            Directory.CreateDirectory(stagingDir);
            var copiedCount = 0;

            foreach (var fileName in edition.TrackedPlatformFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var src = Path.Combine(gameDirectory, fileName);
                if (!File.Exists(src))
                {
                    continue;
                }

                // Do not include EOSSDK-Win64-Shipping*.dll in non-Epic profiles.
                if (fileName.StartsWith("EOSSDK-Win64-Shipping", StringComparison.OrdinalIgnoreCase) &&
                    platform != GamePlatform.EGL)
                {
                    continue;
                }

                progress?.Report($"Backing up {fileName}...");
                var dest = Path.Combine(stagingDir, fileName);
                await CopyFileAsync(src, dest, cancellationToken).ConfigureAwait(false);
                copiedCount++;
                testFailAfterCopyFile?.Invoke(fileName);
            }

            if (platform == GamePlatform.EGL)
            {
                foreach (var eosSrc in GetActiveEpicSdkFiles(gameDirectory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var eosFileName = Path.GetFileName(eosSrc);
                    var eosDest = Path.Combine(stagingDir, eosFileName);
                    if (File.Exists(eosDest))
                    {
                        continue;
                    }

                    progress?.Report($"Backing up {eosFileName}...");
                    await CopyFileAsync(eosSrc, eosDest, cancellationToken).ConfigureAwait(false);
                    copiedCount++;
                    testFailAfterCopyFile?.Invoke(eosFileName);
                }
            }

            if (copiedCount == 0)
            {
                return new OperationResult(false, "No tracked platform files were found to back up.");
            }

            if (Directory.Exists(targetDir))
            {
                previousDir = Path.Combine(parentDir, $".{targetName}_prev_{Guid.NewGuid():N}");
                Directory.Move(targetDir, previousDir);
            }

            Directory.Move(stagingDir, targetDir);

            if (previousDir is not null)
            {
                TryDeleteDirectory(previousDir);
            }

            _logger.Info($"Backed up {copiedCount} platform files for {edition.DisplayName} ({platform}) into '{targetDir}'.");
            return new OperationResult(true, $"Backed up {copiedCount} file(s) to {EditionSpec.GetPlatformDisplayName(platform)} backup.");
        }
        catch (Exception ex)
        {
            if (!Directory.Exists(targetDir) && previousDir is not null && Directory.Exists(previousDir))
            {
                try
                {
                    Directory.Move(previousDir, targetDir);
                }
                catch (Exception restoreEx)
                {
                    _logger.Error($"Failed to restore previous profile '{targetDir}' after backup error.", restoreEx);
                }
            }

            _logger.Error($"Backup for {edition.DisplayName} ({platform}) failed.", ex);
            return new OperationResult(
                false,
                $"Backup failed and existing profile was preserved: {ex.Message}",
                RequiresElevation: ex is UnauthorizedAccessException);
        }
        finally
        {
            TryDeleteDirectory(stagingDir);
        }
    }

    public static OperationResult ValidateActiveFolderForBackup(string gameDirectory, GamePlatform targetPlatform)
    {
        var hasSteamApi = File.Exists(Path.Combine(gameDirectory, "steam_api64.dll"));
        var hasEosSdk = HasActiveEpicSdk(gameDirectory);

        switch (targetPlatform)
        {
            case GamePlatform.Steam:
                if (!hasSteamApi)
                {
                    return new OperationResult(
                        false,
                        "Cannot save a Steam backup because 'steam_api64.dll' was not found in the active game folder. Your game folder currently contains files from another launcher.");
                }
                break;

            case GamePlatform.EGL:
                if (!hasEosSdk)
                {
                    return new OperationResult(
                        false,
                        "Cannot save an Epic Games backup because 'EOSSDK-Win64-Shipping.dll' was not found in the active game folder. Your game folder currently contains files from another launcher.");
                }
                break;

            case GamePlatform.RGL:
                if (hasSteamApi)
                {
                    return new OperationResult(
                        false,
                        "Cannot save a Rockstar Games backup while 'steam_api64.dll' is active in the game folder. Your game folder currently contains Steam files.");
                }

                if (hasEosSdk)
                {
                    return new OperationResult(
                        false,
                        "Cannot save a Rockstar Games backup while 'EOSSDK-Win64-Shipping.dll' is active in the game folder. Your game folder currently contains Epic Games files.");
                }
                break;
        }

        return new OperationResult(true, "Active folder matches target platform signature.");
    }

    public OperationResult CleanInvalidProfile(EditionSpec edition, string gameDirectory, GamePlatform platform)
    {
        if (platform == GamePlatform.Unknown)
        {
            return new OperationResult(false, "Please specify a valid launcher backup to clean.");
        }

        try
        {
            var resolvedDir = _detector.ResolveProfileDirectory(edition, gameDirectory, platform);
            var primaryDir = _detector.GetPrimaryProfileDirectory(edition, gameDirectory, platform);
            var cleanedAny = false;

            foreach (var dir in new[] { resolvedDir, primaryDir }.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                    cleanedAny = true;
                }
            }

            var displayName = EditionSpec.GetPlatformDisplayName(platform);
            if (cleanedAny)
            {
                _logger.Info($"Cleaned invalid {displayName} profile for {edition.DisplayName}.");
                return new OperationResult(true, $"Removed invalid {displayName} backup for {edition.DisplayName}.");
            }

            return new OperationResult(true, $"No invalid {displayName} backup folder was found.");
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to clean invalid profile for {platform}.", ex);
            return new OperationResult(
                false,
                $"Could not remove invalid backup folder: {ex.Message}",
                RequiresElevation: ex is UnauthorizedAccessException);
        }
    }

    public OperationResult RepairSteamApiConflict(string gameDirectory, GamePlatform activePlatform)
    {
        var orig = Path.Combine(gameDirectory, "steam_api64.dll");
        var backup = Path.Combine(gameDirectory, "steam_api64.dll.steam");

        try
        {
            if (File.Exists(orig) && File.Exists(backup))
            {
                if (activePlatform == GamePlatform.Steam)
                {
                    File.Delete(backup);
                    return new OperationResult(true, "Removed duplicate steam_api64.dll.steam (keeping active Steam DLL).");
                }

                File.Delete(orig);
                return new OperationResult(true, "Removed duplicate steam_api64.dll (keeping .steam backup for non-Steam platform).");
            }

            if (activePlatform == GamePlatform.Steam && !File.Exists(orig) && File.Exists(backup))
            {
                File.Move(backup, orig);
                return new OperationResult(true, "Restored steam_api64.dll from steam_api64.dll.steam.");
            }

            if (activePlatform != GamePlatform.Steam && activePlatform != GamePlatform.Unknown &&
                File.Exists(orig) && !File.Exists(backup))
            {
                File.Move(orig, backup);
                return new OperationResult(true, "Moved steam_api64.dll to steam_api64.dll.steam for non-Steam platform.");
            }

            return new OperationResult(true, "No steam_api64.dll conflict found.");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to repair steam_api64.dll conflict.", ex);
            return new OperationResult(false, $"Repair failed: {ex.Message}", ex is UnauthorizedAccessException);
        }
    }

    public OperationResult RepairEpicDllConflict(string gameDirectory, GamePlatform activePlatform)
    {
        try
        {
            var activeFiles = GetActiveEpicSdkFiles(gameDirectory);
            var parkedFiles = GetParkedEpicSdkFiles(gameDirectory);

            if (activePlatform == GamePlatform.EGL)
            {
                if (parkedFiles.Count > 0)
                {
                    var restoredAny = false;
                    foreach (var parkedPath in parkedFiles)
                    {
                        var origPath = parkedPath[..^5];
                        if (File.Exists(origPath))
                        {
                            File.Delete(parkedPath);
                        }
                        else
                        {
                            File.Move(parkedPath, origPath);
                            restoredAny = true;
                        }
                    }

                    return new OperationResult(
                        true,
                        restoredAny
                            ? "Restored Epic Games launcher files from backup."
                            : "Removed duplicate Epic Games .epic backup files.");
                }
            }
            else if (activePlatform != GamePlatform.Unknown)
            {
                if (activeFiles.Count > 0)
                {
                    foreach (var origPath in activeFiles)
                    {
                        var parkedPath = origPath + ".epic";
                        File.Move(origPath, parkedPath, overwrite: true);
                    }

                    return new OperationResult(true, "Parked Epic Games launcher files as .epic backup.");
                }
            }
            else if (activeFiles.Count > 0 && parkedFiles.Count > 0)
            {
                foreach (var origPath in activeFiles)
                {
                    var parkedPath = origPath + ".epic";
                    if (File.Exists(parkedPath))
                    {
                        File.Delete(origPath);
                    }
                }

                return new OperationResult(true, "Removed duplicate Epic Games launcher files.");
            }

            return new OperationResult(true, "No Epic Games launcher file conflict found.");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to repair EOSSDK conflict.", ex);
            return new OperationResult(false, $"Repair failed: {ex.Message}", ex is UnauthorizedAccessException);
        }
    }

    private void UpdateSteamApiDll(string gameDirectory, GamePlatform targetPlatform)
    {
        var orig = Path.Combine(gameDirectory, "steam_api64.dll");
        var backup = Path.Combine(gameDirectory, "steam_api64.dll.steam");

        if (targetPlatform == GamePlatform.Steam)
        {
            if (File.Exists(backup) && !File.Exists(orig))
            {
                File.Move(backup, orig);
            }
            else if (File.Exists(orig) && File.Exists(backup))
            {
                File.Delete(backup);
            }
        }
        else
        {
            if (File.Exists(orig) && !File.Exists(backup))
            {
                File.Move(orig, backup);
            }
            else if (File.Exists(orig) && File.Exists(backup))
            {
                File.Delete(orig);
            }
        }
    }

    private void UpdateEpicSdkDll(string gameDirectory, GamePlatform targetPlatform, string sourceProfileDir)
    {
        if (targetPlatform == GamePlatform.EGL)
        {
            foreach (var parkedPath in GetParkedEpicSdkFiles(gameDirectory))
            {
                var origPath = parkedPath[..^5];
                if (!File.Exists(origPath))
                {
                    File.Move(parkedPath, origPath);
                }
                else
                {
                    File.Delete(parkedPath);
                }
            }
        }
        else
        {
            var profileEosNames = GetActiveEpicSdkFiles(sourceProfileDir)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var origPath in GetActiveEpicSdkFiles(gameDirectory))
            {
                var fileName = Path.GetFileName(origPath);
                if (!profileEosNames.Contains(fileName))
                {
                    var parkedPath = origPath + ".epic";
                    File.Move(origPath, parkedPath, overwrite: true);
                }
            }
        }
    }

    private void Rollback(
        IEnumerable<(string OriginalPath, string BackupPath)> backedUpTargets,
        IEnumerable<string> copiedNewFiles,
        IEnumerable<DllRollbackEntry> dllSnapshots)
    {
        foreach (var newFile in copiedNewFiles)
        {
            try
            {
                if (File.Exists(newFile))
                {
                    File.Delete(newFile);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Rollback failed to delete new file '{newFile}'.", ex);
            }
        }

        foreach (var (originalPath, backupPath) in backedUpTargets)
        {
            try
            {
                if (File.Exists(backupPath))
                {
                    File.Copy(backupPath, originalPath, overwrite: true);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Rollback failed to restore '{originalPath}'.", ex);
            }
        }

        foreach (var dll in dllSnapshots)
        {
            try
            {
                if (dll.ExistedBefore && dll.BackupPath is not null && File.Exists(dll.BackupPath))
                {
                    File.Copy(dll.BackupPath, dll.TargetPath, overwrite: true);
                }
                else if (!dll.ExistedBefore && File.Exists(dll.TargetPath))
                {
                    File.Delete(dll.TargetPath);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Rollback failed to restore runtime DLL '{dll.TargetPath}'.", ex);
            }
        }
    }

    private static async Task CopyFileAsync(string source, string destination, CancellationToken cancellationToken)
    {
        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
    }

    private static string ReadExecutableVersion(string exePath)
    {
        if (!File.Exists(exePath))
        {
            return "Unknown";
        }

        try
        {
            var info = FileVersionInfo.GetVersionInfo(exePath);
            if (!string.IsNullOrWhiteSpace(info.FileVersion))
            {
                return info.FileVersion.Trim();
            }

            if (!string.IsNullOrWhiteSpace(info.ProductVersion))
            {
                return info.ProductVersion.Trim();
            }

            return "Present";
        }
        catch
        {
            return "Present";
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Ignore temp cleanup failure.
        }
    }
}
