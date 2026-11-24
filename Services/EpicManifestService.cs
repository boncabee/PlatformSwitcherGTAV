using System.Text.Json;
using System.Text.Json.Nodes;
using Grand_Theft_Auto_V_Platform_Switcher.Models;

namespace Grand_Theft_Auto_V_Platform_Switcher.Services;

public enum EpicManifestState
{
    NotFound,
    PointsElsewhere,
    AlignedMissingEgstore,
    Aligned
}

public sealed record EpicManifestInspection(
    EpicManifestState State,
    string? ManifestFilePath,
    string? AppName,
    string? DisplayName,
    string? CurrentInstallLocation,
    bool HasSourceEgstore,
    bool HasTargetEgstore,
    bool IsLauncherInstalledAligned,
    bool IsEpicRunning,
    bool IsIncompleteInstall = false,
    bool HasOrphanDuplicates = false);

public sealed class EpicManifestService
{
    private const string DefaultRockstarCatalogNamespace = "0584d2013f0149a791e7b9bad0eec102";
    private const string DefaultEnhancedEpicAppId = "8769e24080ea413b8ebca3f1b8c50951";

    private readonly ProcessGuard _processGuard;
    private readonly AppLogger _logger;
    private readonly string _epicManifestsDirectory;
    private readonly string _launcherInstalledFilePath;
    private readonly string _eoshInstalledItemsDirectory;

    public EpicManifestService(
        ProcessGuard processGuard,
        AppLogger logger,
        string? epicManifestsDirectory = null,
        string? launcherInstalledFilePath = null,
        string? eoshInstalledItemsDirectory = null)
    {
        _processGuard = processGuard;
        _logger = logger;

        var commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var hasCustomManifestDir = !string.IsNullOrWhiteSpace(epicManifestsDirectory);

        _epicManifestsDirectory = hasCustomManifestDir
            ? epicManifestsDirectory!
            : Path.Combine(commonAppData, "Epic", "EpicGamesLauncher", "Data", "Manifests");

        var sandboxRoot = hasCustomManifestDir
            ? Path.GetDirectoryName(Path.GetFullPath(_epicManifestsDirectory.TrimEnd('\\', '/'))) ?? _epicManifestsDirectory
            : Path.Combine(commonAppData, "Epic");

        _launcherInstalledFilePath = !string.IsNullOrWhiteSpace(launcherInstalledFilePath)
            ? launcherInstalledFilePath
            : Path.Combine(sandboxRoot, "UnrealEngineLauncher", "LauncherInstalled.dat");

        _eoshInstalledItemsDirectory = !string.IsNullOrWhiteSpace(eoshInstalledItemsDirectory)
            ? eoshInstalledItemsDirectory
            : Path.Combine(sandboxRoot, "EpicOnlineServicesShared", "InstallHelper", "InstalledItems");
    }

    public static bool HasEgstoreDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        var egstoreDir = Path.Combine(directory, ".egstore");
        if (!Directory.Exists(egstoreDir))
        {
            return false;
        }

        return Directory.EnumerateFiles(egstoreDir, "*", SearchOption.AllDirectories)
            .Any(file => !IsBpsRelativePath(Path.GetRelativePath(egstoreDir, file)));
    }

    public EpicManifestInspection Inspect(
        EditionSpec edition,
        string? gameDirectory,
        string? epicAppIdOverride = null)
    {
        var isEpicRunning = _processGuard.IsEpicGamesLauncherRunning();
        var hasTargetEgstore = HasEgstoreDirectory(gameDirectory);
        var allCandidates = FindCandidateItemManifests(edition, epicAppIdOverride);
        var match = SelectPrimaryManifest(allCandidates, gameDirectory);

        if (match is null)
        {
            return new EpicManifestInspection(
                EpicManifestState.NotFound,
                ManifestFilePath: null,
                AppName: null,
                DisplayName: null,
                CurrentInstallLocation: null,
                HasSourceEgstore: false,
                HasTargetEgstore: hasTargetEgstore,
                IsLauncherInstalledAligned: false,
                IsEpicRunning: isEpicRunning);
        }

        var primary = match.Value;
        var orphanPendingItems = allCandidates
            .Where(c => !ArePathsEqual(c.FilePath, primary.FilePath) && c.IsPendingFolder)
            .ToList();

        var eoshEntries = FindEoshEntries(primary.AppName, primary.InstallationGuid);
        var primaryEosh = SelectPrimaryEoshEntry(eoshEntries, primary.InstallationGuid, gameDirectory);
        var orphanEoshEntries = eoshEntries
            .Where(e => primaryEosh is null || !ArePathsEqual(e.FilePath, primaryEosh.Value.FilePath))
            .Where(e => string.Equals(e.State, "Incomplete", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var hasOrphanDuplicates = orphanPendingItems.Count > 0 || orphanEoshEntries.Count > 0;

        var effectiveCurrentInstallLocation = primary.InstallLocation;
        if (!string.IsNullOrWhiteSpace(gameDirectory) &&
            ArePathsEqual(primary.InstallLocation, gameDirectory) &&
            primaryEosh is not null &&
            !string.IsNullOrWhiteSpace(primaryEosh.Value.Dir) &&
            !ArePathsEqual(primaryEosh.Value.Dir, gameDirectory))
        {
            effectiveCurrentInstallLocation = primaryEosh.Value.Dir;
        }

        var hasSourceEgstore = HasEgstoreDirectory(effectiveCurrentInstallLocation) ||
                               HasEgstoreDirectory(primary.InstallLocation) ||
                               (primaryEosh is not null && HasEgstoreDirectory(primaryEosh.Value.Dir));

        var isItemAligned = !string.IsNullOrWhiteSpace(gameDirectory) &&
                            IsItemEntryAligned(primary, gameDirectory);
        var isLauncherDatAligned = !string.IsNullOrWhiteSpace(gameDirectory) &&
                                   IsLauncherInstalledDatAligned(primary.AppName, gameDirectory);
        var isEoshAligned = !string.IsNullOrWhiteSpace(gameDirectory) &&
                            (primaryEosh is null || IsEoshEntryAligned(primaryEosh.Value, gameDirectory));

        EpicManifestState state;
        if (!isItemAligned || !isLauncherDatAligned || !isEoshAligned || hasOrphanDuplicates)
        {
            state = EpicManifestState.PointsElsewhere;
        }
        else if (!hasTargetEgstore)
        {
            state = EpicManifestState.AlignedMissingEgstore;
        }
        else
        {
            state = EpicManifestState.Aligned;
        }

        return new EpicManifestInspection(
            state,
            primary.FilePath,
            primary.AppName,
            primary.DisplayName,
            effectiveCurrentInstallLocation,
            hasSourceEgstore,
            hasTargetEgstore,
            isLauncherDatAligned,
            isEpicRunning,
            IsIncompleteInstall: primary.IsIncompleteInstall,
            HasOrphanDuplicates: hasOrphanDuplicates);
    }

    public Task<OperationResult> RelocateAndAdoptAsync(
        EditionSpec edition,
        string gameDirectory,
        PlatformSwitchService switchService,
        string? epicAppIdOverride = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default,
        Action? testFailBeforeLauncherInstalledWrite = null) =>
        RelocateAndAdoptCoreAsync(
            edition,
            gameDirectory,
            sourceEpicFolderOverride: null,
            switchService,
            epicAppIdOverride,
            progress,
            cancellationToken,
            testFailBeforeLauncherInstalledWrite);

    private async Task<OperationResult> RelocateAndAdoptCoreAsync(
        EditionSpec edition,
        string gameDirectory,
        string? sourceEpicFolderOverride,
        PlatformSwitchService switchService,
        string? epicAppIdOverride,
        IProgress<string>? progress,
        CancellationToken cancellationToken,
        Action? testFailBeforeLauncherInstalledWrite)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) ||
            !Directory.Exists(gameDirectory) ||
            !File.Exists(Path.Combine(gameDirectory, edition.MainExecutable)))
        {
            return new OperationResult(false, $"Could not find {edition.MainExecutable} in '{gameDirectory}'.");
        }

        if (_processGuard.IsEpicGamesLauncherRunning())
        {
            return new OperationResult(
                false,
                "Epic Games Launcher is currently running. Please close Epic Games Launcher completely (including from the system tray) before adopting the installation.");
        }

        var inspection = Inspect(edition, gameDirectory, epicAppIdOverride);
        if (inspection.State == EpicManifestState.NotFound ||
            string.IsNullOrWhiteSpace(inspection.ManifestFilePath) ||
            string.IsNullOrWhiteSpace(inspection.AppName))
        {
            return new OperationResult(
                false,
                $"No Epic Games Launcher installation manifest was found for {edition.DisplayName}. " +
                $"Start installing {edition.DisplayName} in Epic Games Launcher to any empty folder for a few seconds, pause the download, close Epic Games Launcher, and try again.");
        }

        var preferredSourceFolder = !string.IsNullOrWhiteSpace(sourceEpicFolderOverride)
            ? sourceEpicFolderOverride
            : inspection.CurrentInstallLocation;
        var hasPreferredSourceEgstore = HasEgstoreDirectory(preferredSourceFolder);

        if (!inspection.HasTargetEgstore && !hasPreferredSourceEgstore && !inspection.HasSourceEgstore)
        {
            return new OperationResult(
                false,
                $"Could not find the '.egstore' metadata folder in '{preferredSourceFolder}' or '{gameDirectory}'. " +
                $"Start installing {edition.DisplayName} in Epic Games Launcher for a few seconds so it generates '.egstore', pause it, close Epic Games Launcher, and try again.");
        }

        var steamApiPath = Path.Combine(gameDirectory, "steam_api64.dll");
        var steamApiBackupPath = Path.Combine(gameDirectory, "steam_api64.dll.steam");
        var hasActiveEosSdk = PlatformSwitchService.HasActiveEpicSdk(gameDirectory);
        var versions = switchService.GetVersionSnapshot(edition, gameDirectory);

        if (File.Exists(steamApiPath) && !versions.HasSteamProfile)
        {
            progress?.Report("Saving automatic backup of your current Steam files...");
            var backupRes = await switchService.BackupCurrentPlatformAsync(
                edition,
                gameDirectory,
                GamePlatform.Steam,
                progress,
                cancellationToken).ConfigureAwait(false);

            if (!backupRes.Success)
            {
                return new OperationResult(
                    false,
                    $"Could not save an automatic backup of your Steam files before adopting for Epic Games: {backupRes.Message}",
                    backupRes.RequiresElevation);
            }
        }
        else if (!File.Exists(steamApiPath) &&
                 !File.Exists(steamApiBackupPath) &&
                 !hasActiveEosSdk &&
                 !versions.HasRglProfile)
        {
            progress?.Report("Saving automatic backup of your current Rockstar Games files...");
            var rglBackup = await switchService.BackupCurrentPlatformAsync(
                edition,
                gameDirectory,
                GamePlatform.RGL,
                progress,
                cancellationToken).ConfigureAwait(false);

            if (!rglBackup.Success)
            {
                return new OperationResult(
                    false,
                    $"Could not save an automatic backup of your Rockstar Games files before adopting for Epic Games: {rglBackup.Message}",
                    rglBackup.RequiresElevation);
            }
        }

        var manifestPath = inspection.ManifestFilePath;
        var fileSnapshots = new List<FileByteSnapshot>();
        CaptureFileSnapshot(fileSnapshots, manifestPath);
        CaptureFileSnapshot(fileSnapshots, _launcherInstalledFilePath);
        CaptureFileSnapshot(fileSnapshots, steamApiPath);
        CaptureFileSnapshot(fileSnapshots, steamApiBackupPath);

        var allCandidates = FindCandidateItemManifests(edition, epicAppIdOverride);
        var primaryCandidate = SelectPrimaryManifest(allCandidates, gameDirectory);
        var primaryGuid = primaryCandidate?.InstallationGuid;

        foreach (var candidate in allCandidates)
        {
            if (!ArePathsEqual(candidate.FilePath, manifestPath) && candidate.IsPendingFolder)
            {
                CaptureFileSnapshot(fileSnapshots, candidate.FilePath);
            }
        }

        var eoshEntries = FindEoshEntries(inspection.AppName, primaryGuid);
        foreach (var eosh in eoshEntries)
        {
            CaptureFileSnapshot(fileSnapshots, eosh.FilePath);
        }

        var targetEgstoreDir = Path.Combine(gameDirectory, ".egstore");
        var targetEgstoreExistedBefore = Directory.Exists(targetEgstoreDir);
        var targetBpsDir = Path.Combine(targetEgstoreDir, "bps");
        var targetBpsExistedBefore = Directory.Exists(targetBpsDir);
        if (targetBpsExistedBefore && !hasActiveEosSdk)
        {
            foreach (var bpsFile in Directory.GetFiles(targetBpsDir, "*", SearchOption.AllDirectories))
            {
                CaptureFileSnapshot(fileSnapshots, bpsFile);
            }
        }

        var copiedEgstoreFiles = new List<string>();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 1. Copy .egstore metadata (excluding bps resume state) if needed
            var effectiveSourceDir = ResolveSourceEgstoreParent(preferredSourceFolder, primaryCandidate, eoshEntries, gameDirectory);
            if (!string.IsNullOrWhiteSpace(effectiveSourceDir) &&
                !ArePathsEqual(effectiveSourceDir, gameDirectory) &&
                HasEgstoreDirectory(effectiveSourceDir))
            {
                var sourceEgstore = Path.Combine(effectiveSourceDir, ".egstore");
                var sourceFiles = Directory.GetFiles(sourceEgstore, "*", SearchOption.AllDirectories)
                    .Where(src => !IsBpsRelativePath(Path.GetRelativePath(sourceEgstore, src)))
                    .ToArray();

                progress?.Report($"Copying Epic Games metadata (.egstore, {sourceFiles.Length} file(s))...");
                foreach (var srcFile in sourceFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var relative = Path.GetRelativePath(sourceEgstore, srcFile);
                    var destFile = Path.Combine(targetEgstoreDir, relative);
                    var destExisted = File.Exists(destFile);
                    if (destExisted)
                    {
                        CaptureFileSnapshot(fileSnapshots, destFile);
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
                    await CopyFileAsync(srcFile, destFile, cancellationToken).ConfigureAwait(false);

                    if (!destExisted)
                    {
                        copiedEgstoreFiles.Add(destFile);
                    }
                }
            }

            // Remove stale .egstore\bps resume data so Epic/EOSH verifies existing game files instead of downloading from 0%
            if (!hasActiveEosSdk && Directory.Exists(targetBpsDir))
            {
                Directory.Delete(targetBpsDir, recursive: true);
            }

            // 2. Park active steam_api64.dll so Epic verification/launch does not load Steam API
            if (File.Exists(steamApiPath))
            {
                progress?.Report("Parking steam_api64.dll as steam_api64.dll.steam...");
                if (File.Exists(steamApiBackupPath))
                {
                    File.Delete(steamApiPath);
                }
                else
                {
                    File.Move(steamApiPath, steamApiBackupPath);
                }
            }

            // 3. Update .item manifest in-place using JsonNode (all 5 path fields)
            progress?.Report("Updating Epic Games Launcher .item manifest...");
            var itemRoot = UpdateItemManifestFile(manifestPath, gameDirectory);

            // 4. Update EOSH InstalledItems\*.egi and clean orphaned duplicate Pending manifests
            progress?.Report("Syncing Epic Online Services InstallHelper state...");
            SyncEoshAndCleanOrphans(
                inspection.AppName,
                primaryGuid,
                manifestPath,
                gameDirectory,
                allCandidates,
                eoshEntries);

            testFailBeforeLauncherInstalledWrite?.Invoke();

            // 5. Update LauncherInstalled.dat in-place using JsonNode
            progress?.Report("Updating Epic Games LauncherInstalled.dat...");
            UpdateLauncherInstalledFile(inspection.AppName, gameDirectory, itemRoot);

            _logger.Info($"Adopted Epic Games installation for {edition.DisplayName} into '{gameDirectory}' (Manifest: '{manifestPath}').");

            var hasEpicSdk = PlatformSwitchService.HasActiveEpicSdk(gameDirectory);
            var message = hasEpicSdk
                ? $"Epic Games Launcher is now linked to '{gameDirectory}'. You can now open Epic Games Launcher and launch {edition.DisplayName}."
                : $"Epic Games Launcher is now pointed to '{gameDirectory}' and your current launcher files were backed up safely.\n\n" +
                  "Next steps:\n" +
                  $"1. Open Epic Games Launcher and click Resume / Verify on {edition.DisplayName} (it will verify your existing files and download only Epic launcher files).\n" +
                  "2. Once Epic Games finishes verifying, return to this app to save your Epic Games backup.";

            return new OperationResult(true, message);
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to adopt Epic Games installation for {edition.DisplayName}. Rolling back changes...", ex);
            RollbackSnapshots(
                fileSnapshots,
                copiedEgstoreFiles,
                targetEgstoreDir,
                targetEgstoreExistedBefore);

            return new OperationResult(
                false,
                $"Failed to adopt Epic Games installation and all changes were rolled back: {ex.Message}",
                RequiresElevation: ex is UnauthorizedAccessException);
        }
    }

    public OperationResult SyncManifestPath(
        EditionSpec edition,
        string gameDirectory,
        string? epicAppIdOverride = null)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || !Directory.Exists(gameDirectory))
        {
            return new OperationResult(false, "Game directory does not exist.");
        }

        if (_processGuard.IsEpicGamesLauncherRunning())
        {
            return new OperationResult(false, "Epic Games Launcher is currently running; skipped manifest path sync.");
        }

        var inspection = Inspect(edition, gameDirectory, epicAppIdOverride);
        if (inspection.State == EpicManifestState.NotFound ||
            string.IsNullOrWhiteSpace(inspection.ManifestFilePath) ||
            string.IsNullOrWhiteSpace(inspection.AppName))
        {
            return new OperationResult(false, "No Epic Games Launcher manifest was found to sync.");
        }

        if (inspection.State == EpicManifestState.Aligned)
        {
            return new OperationResult(true, "Epic Games Launcher manifest is already aligned.");
        }

        var manifestPath = inspection.ManifestFilePath;
        var fileSnapshots = new List<FileByteSnapshot>();
        CaptureFileSnapshot(fileSnapshots, manifestPath);
        CaptureFileSnapshot(fileSnapshots, _launcherInstalledFilePath);

        var allCandidates = FindCandidateItemManifests(edition, epicAppIdOverride);
        var primaryCandidate = SelectPrimaryManifest(allCandidates, gameDirectory);
        var primaryGuid = primaryCandidate?.InstallationGuid;

        foreach (var candidate in allCandidates)
        {
            if (!ArePathsEqual(candidate.FilePath, manifestPath) && candidate.IsPendingFolder)
            {
                CaptureFileSnapshot(fileSnapshots, candidate.FilePath);
            }
        }

        var eoshEntries = FindEoshEntries(inspection.AppName, primaryGuid);
        foreach (var eosh in eoshEntries)
        {
            CaptureFileSnapshot(fileSnapshots, eosh.FilePath);
        }

        var copiedEgstoreFiles = new List<string>();
        var targetEgstoreDir = Path.Combine(gameDirectory, ".egstore");
        var targetEgstoreExistedBefore = Directory.Exists(targetEgstoreDir);

        try
        {
            var effectiveSourceDir = ResolveSourceEgstoreParent(inspection.CurrentInstallLocation, primaryCandidate, eoshEntries, gameDirectory);
            if (!inspection.HasTargetEgstore &&
                !string.IsNullOrWhiteSpace(effectiveSourceDir) &&
                !ArePathsEqual(effectiveSourceDir, gameDirectory) &&
                HasEgstoreDirectory(effectiveSourceDir))
            {
                var sourceEgstore = Path.Combine(effectiveSourceDir, ".egstore");
                foreach (var srcFile in Directory.GetFiles(sourceEgstore, "*", SearchOption.AllDirectories))
                {
                    var relative = Path.GetRelativePath(sourceEgstore, srcFile);
                    if (IsBpsRelativePath(relative))
                    {
                        continue;
                    }

                    var destFile = Path.Combine(targetEgstoreDir, relative);
                    var destExisted = File.Exists(destFile);
                    if (destExisted)
                    {
                        CaptureFileSnapshot(fileSnapshots, destFile);
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
                    File.Copy(srcFile, destFile, overwrite: true);
                    if (!destExisted)
                    {
                        copiedEgstoreFiles.Add(destFile);
                    }
                }
            }

            var itemRoot = UpdateItemManifestFile(manifestPath, gameDirectory);
            SyncEoshAndCleanOrphans(
                inspection.AppName,
                primaryGuid,
                manifestPath,
                gameDirectory,
                allCandidates,
                eoshEntries);
            UpdateLauncherInstalledFile(inspection.AppName, gameDirectory, itemRoot);
            _logger.Info($"Synced Epic Games Launcher manifest for {edition.DisplayName} to '{gameDirectory}'.");
            return new OperationResult(true, $"Synced Epic Games Launcher manifest to '{gameDirectory}'.");
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed to sync Epic Games Launcher manifest: {ex.Message}");
            RollbackSnapshots(fileSnapshots, copiedEgstoreFiles, targetEgstoreDir, targetEgstoreExistedBefore);
            return new OperationResult(false, $"Could not sync Epic Games Launcher manifest: {ex.Message}", ex is UnauthorizedAccessException);
        }
    }

    public async Task<OperationResult> RelocateAndAdoptFromFolderAsync(
        EditionSpec edition,
        string gameDirectory,
        string sourceEpicFolder,
        PlatformSwitchService switchService,
        string? epicAppIdOverride = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) ||
            !Directory.Exists(gameDirectory) ||
            !File.Exists(Path.Combine(gameDirectory, edition.MainExecutable)))
        {
            return new OperationResult(false, $"Could not find {edition.MainExecutable} in '{gameDirectory}'.");
        }

        if (_processGuard.IsEpicGamesLauncherRunning())
        {
            return new OperationResult(
                false,
                "Epic Games Launcher is currently running. Please close Epic Games Launcher completely (including from the system tray) before adopting the installation.");
        }

        if (string.IsNullOrWhiteSpace(sourceEpicFolder) || !Directory.Exists(sourceEpicFolder))
        {
            return new OperationResult(false, "The selected temporary Epic Games folder does not exist.");
        }

        // Allow selecting either the parent folder or the .egstore subfolder directly
        var normalizedSource = Path.GetFullPath(sourceEpicFolder).TrimEnd('\\', '/');
        if (string.Equals(Path.GetFileName(normalizedSource), ".egstore", StringComparison.OrdinalIgnoreCase))
        {
            normalizedSource = Path.GetDirectoryName(normalizedSource) ?? normalizedSource;
        }

        if (!HasEgstoreDirectory(normalizedSource))
        {
            return new OperationResult(
                false,
                $"Could not find an '.egstore' folder inside '{normalizedSource}'.\n\n" +
                $"Make sure you started installing {edition.DisplayName} in Epic Games Launcher for at least 5-10 seconds into that folder before pausing.");
        }

        var inspection = Inspect(edition, gameDirectory, epicAppIdOverride);
        if (inspection.State == EpicManifestState.NotFound)
        {
            EnsureBootstrapItemManifestFromEgstore(edition, normalizedSource, epicAppIdOverride);
        }

        return await RelocateAndAdoptCoreAsync(
            edition,
            gameDirectory,
            sourceEpicFolderOverride: normalizedSource,
            switchService,
            epicAppIdOverride,
            progress,
            cancellationToken,
            testFailBeforeLauncherInstalledWrite: null).ConfigureAwait(false);
    }

    private void EnsureBootstrapItemManifestFromEgstore(
        EditionSpec edition,
        string sourceEpicFolder,
        string? epicAppIdOverride)
    {
        var egstoreDir = Path.Combine(sourceEpicFolder, ".egstore");
        var appName = !string.IsNullOrWhiteSpace(epicAppIdOverride)
            ? epicAppIdOverride.Trim()
            : !string.IsNullOrWhiteSpace(edition.DefaultEpicAppId)
                ? edition.DefaultEpicAppId
                : edition.Edition == GameEdition.Enhanced
                    ? DefaultEnhancedEpicAppId
                    : "9d2d0eb64d5c44529cece33fe2a46482";
        var catalogNamespace = DefaultRockstarCatalogNamespace;
        var catalogItemId = string.Empty;

        var mancpnFile = Directory.EnumerateFiles(egstoreDir, "*.mancpn", SearchOption.AllDirectories).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(mancpnFile))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(mancpnFile));
                var root = doc.RootElement;
                if (root.TryGetProperty("AppName", out var appProp) && !string.IsNullOrWhiteSpace(appProp.GetString()))
                {
                    appName = appProp.GetString()!.Trim();
                }
                if (root.TryGetProperty("CatalogNamespace", out var nsProp) && !string.IsNullOrWhiteSpace(nsProp.GetString()))
                {
                    catalogNamespace = nsProp.GetString()!.Trim();
                }
                if (root.TryGetProperty("CatalogItemId", out var itemProp) && !string.IsNullOrWhiteSpace(itemProp.GetString()))
                {
                    catalogItemId = itemProp.GetString()!.Trim();
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"Could not parse .mancpn '{mancpnFile}': {ex.Message}");
            }
        }

        var manifestFile = Directory.EnumerateFiles(egstoreDir, "*.manifest", SearchOption.AllDirectories).FirstOrDefault();
        var installationGuid = !string.IsNullOrWhiteSpace(manifestFile)
            ? Path.GetFileNameWithoutExtension(manifestFile)
            : !string.IsNullOrWhiteSpace(catalogItemId)
                ? catalogItemId
                : Guid.NewGuid().ToString("N").ToUpperInvariant();

        Directory.CreateDirectory(_epicManifestsDirectory);
        var itemPath = Path.Combine(_epicManifestsDirectory, $"{installationGuid}.item");
        var forwardSource = sourceEpicFolder.Replace('\\', '/');

        var itemObj = new JsonObject
        {
            ["FormatVersion"] = 0,
            ["bIsIncompleteInstall"] = true,
            ["LaunchCommand"] = string.Empty,
            ["LaunchExecutable"] = "PlayGTAV.exe",
            ["ManifestLocation"] = $"{forwardSource}/.egstore",
            ["CompleteManifestPath"] = Path.Combine(sourceEpicFolder, ".egstore", $"{installationGuid}.manifest"),
            ["PendingManifestPath"] = Path.Combine(sourceEpicFolder, ".egstore", "Pending", $"{installationGuid}.manifest"),
            ["StagingLocation"] = $"{forwardSource}/.egstore/bps",
            ["bIsApplication"] = true,
            ["bIsExecutable"] = true,
            ["bIsManaged"] = true,
            ["bNeedsValidation"] = true,
            ["bRequiresAuth"] = true,
            ["bAllowMultipleInstances"] = false,
            ["bCanRunOffline"] = true,
            ["AppName"] = appName,
            ["DisplayName"] = edition.DisplayName,
            ["InstallationGuid"] = installationGuid,
            ["InstallLocation"] = sourceEpicFolder,
            ["CatalogNamespace"] = catalogNamespace,
            ["CatalogItemId"] = catalogItemId
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(itemPath, itemObj.ToJsonString(options));
    }

    private JsonObject UpdateItemManifestFile(string manifestPath, string gameDirectory)
    {
        var rawJson = File.ReadAllText(manifestPath);
        var node = JsonNode.Parse(rawJson) as JsonObject
                   ?? throw new InvalidDataException($"Manifest '{manifestPath}' does not contain a valid JSON object.");

        var normalizedGameDir = Path.GetFullPath(gameDirectory).TrimEnd('\\', '/');
        var forwardGameDir = normalizedGameDir.Replace('\\', '/');

        node["InstallLocation"] = normalizedGameDir;
        node["ManifestLocation"] = $"{forwardGameDir}/.egstore";
        node["StagingLocation"] = $"{forwardGameDir}/.egstore/bps";

        var installationGuid = node["InstallationGuid"]?.GetValue<string>()?.Trim();
        var existingComplete = node["CompleteManifestPath"]?.GetValue<string>()?.Trim();
        if (!string.IsNullOrWhiteSpace(existingComplete) || !string.IsNullOrWhiteSpace(installationGuid))
        {
            var completeFileName = !string.IsNullOrWhiteSpace(existingComplete)
                ? Path.GetFileName(existingComplete)
                : $"{installationGuid}.manifest";
            node["CompleteManifestPath"] = Path.Combine(normalizedGameDir, ".egstore", completeFileName);
        }

        var existingPending = node["PendingManifestPath"]?.GetValue<string>()?.Trim();
        if (!string.IsNullOrWhiteSpace(existingPending) || !string.IsNullOrWhiteSpace(installationGuid))
        {
            var pendingFileName = !string.IsNullOrWhiteSpace(existingPending)
                ? Path.GetFileName(existingPending)
                : $"{installationGuid}.manifest";
            node["PendingManifestPath"] = Path.Combine(normalizedGameDir, ".egstore", "Pending", pendingFileName);
        }

        if (PlatformSwitchService.HasActiveEpicSdk(normalizedGameDir))
        {
            node["bIsIncompleteInstall"] = false;
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(manifestPath, node.ToJsonString(options));
        return node;
    }

    private void SyncEoshAndCleanOrphans(
        string appName,
        string? primaryInstallationGuid,
        string primaryManifestPath,
        string gameDirectory,
        IReadOnlyList<ItemManifestEntry> allCandidates,
        IReadOnlyList<EoshItemEntry> eoshEntries)
    {
        // 1. Update primary EOSH .egi file if present, and remove duplicate Incomplete .egi files for the same appName
        var primaryEosh = SelectPrimaryEoshEntry(eoshEntries, primaryInstallationGuid, gameDirectory);
        if (primaryEosh is not null && File.Exists(primaryEosh.Value.FilePath))
        {
            UpdateEoshItemFile(primaryEosh.Value.FilePath, gameDirectory);
        }

        foreach (var eosh in eoshEntries)
        {
            if (primaryEosh is not null && ArePathsEqual(eosh.FilePath, primaryEosh.Value.FilePath))
            {
                continue;
            }

            if (string.Equals(eosh.State, "Incomplete", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(eosh.FilePath))
            {
                File.Delete(eosh.FilePath);
                _logger.Info($"Removed orphaned duplicate EOSH Incomplete entry '{eosh.FilePath}'.");
            }
        }

        // 2. Remove duplicate Pending\*.item manifests for the same edition/appName
        foreach (var candidate in allCandidates)
        {
            if (ArePathsEqual(candidate.FilePath, primaryManifestPath))
            {
                continue;
            }

            if (candidate.IsPendingFolder && File.Exists(candidate.FilePath))
            {
                File.Delete(candidate.FilePath);
                _logger.Info($"Removed orphaned duplicate Pending Epic manifest '{candidate.FilePath}'.");
            }
        }
    }

    private static void UpdateEoshItemFile(string egiFilePath, string gameDirectory)
    {
        var rawJson = File.ReadAllText(egiFilePath);
        var root = JsonNode.Parse(rawJson) as JsonObject
                   ?? throw new InvalidDataException($"EOSH item '{egiFilePath}' does not contain a valid JSON object.");

        if (root["v4"] is not JsonObject v4)
        {
            return;
        }

        var normalizedGameDir = Path.GetFullPath(gameDirectory).TrimEnd('\\', '/');
        var installationId = v4["installationId"]?.GetValue<string>()?.Trim();
        if (string.IsNullOrWhiteSpace(installationId))
        {
            installationId = Path.GetFileNameWithoutExtension(egiFilePath);
        }

        var existingManifestPath = v4["manifestPath"]?.GetValue<string>()?.Trim();
        var manifestFileName = !string.IsNullOrWhiteSpace(existingManifestPath)
            ? Path.GetFileName(existingManifestPath)
            : $"{installationId}.manifest";

        var existingPendingPath = v4["pendingManifestPath"]?.GetValue<string>()?.Trim();
        var pendingFileName = !string.IsNullOrWhiteSpace(existingPendingPath)
            ? Path.GetFileName(existingPendingPath)
            : $"{installationId}.manifest";

        v4["dir"] = normalizedGameDir;
        v4["metaDir"] = Path.Combine(normalizedGameDir, ".egstore");
        v4["manifestPath"] = Path.Combine(normalizedGameDir, ".egstore", manifestFileName);
        v4["pendingManifestPath"] = Path.Combine(normalizedGameDir, ".egstore", "Pending", pendingFileName);

        File.WriteAllText(egiFilePath, root.ToJsonString());
    }

    private void UpdateLauncherInstalledFile(string appName, string gameDirectory, JsonObject itemRoot)
    {
        var normalizedGameDir = Path.GetFullPath(gameDirectory).TrimEnd('\\', '/');
        JsonObject rootObj;

        if (File.Exists(_launcherInstalledFilePath))
        {
            var raw = File.ReadAllText(_launcherInstalledFilePath);
            rootObj = string.IsNullOrWhiteSpace(raw)
                ? new JsonObject()
                : JsonNode.Parse(raw) as JsonObject ?? new JsonObject();
        }
        else
        {
            rootObj = new JsonObject();
        }

        if (rootObj["InstallationList"] is not JsonArray list)
        {
            list = new JsonArray();
            rootObj["InstallationList"] = list;
        }

        JsonObject? matchedEntry = null;
        foreach (var element in list)
        {
            if (element is JsonObject entry &&
                string.Equals(entry["AppName"]?.GetValue<string>(), appName, StringComparison.OrdinalIgnoreCase))
            {
                matchedEntry = entry;
                break;
            }
        }

        if (matchedEntry is not null)
        {
            matchedEntry["InstallLocation"] = normalizedGameDir;
        }
        else
        {
            var newEntry = new JsonObject
            {
                ["InstallLocation"] = normalizedGameDir,
                ["NamespaceId"] = itemRoot["CatalogNamespace"]?.GetValue<string>() ?? DefaultRockstarCatalogNamespace,
                ["ItemId"] = itemRoot["CatalogItemId"]?.GetValue<string>() ?? string.Empty,
                ["ArtifactId"] = appName,
                ["AppVersion"] = itemRoot["AppVersionString"]?.GetValue<string>() ?? string.Empty,
                ["AppName"] = appName
            };
            list.Add(newEntry);
        }

        var dir = Path.GetDirectoryName(_launcherInstalledFilePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(_launcherInstalledFilePath, rootObj.ToJsonString(options));
    }

    private static bool IsItemEntryAligned(ItemManifestEntry entry, string gameDirectory)
    {
        var egstoreDir = Path.Combine(gameDirectory, ".egstore");
        var pendingDir = Path.Combine(egstoreDir, "Pending");

        if (!ArePathsEqual(entry.InstallLocation, gameDirectory))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(entry.ManifestLocation) &&
            !ArePathsEqual(entry.ManifestLocation, egstoreDir))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(entry.CompleteManifestPath) &&
            !ArePathsEqual(Path.GetDirectoryName(entry.CompleteManifestPath), egstoreDir))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(entry.PendingManifestPath) &&
            !ArePathsEqual(Path.GetDirectoryName(entry.PendingManifestPath), pendingDir))
        {
            return false;
        }

        return true;
    }

    private static bool IsEoshEntryAligned(EoshItemEntry entry, string gameDirectory)
    {
        var egstoreDir = Path.Combine(gameDirectory, ".egstore");
        var pendingDir = Path.Combine(egstoreDir, "Pending");

        if (!ArePathsEqual(entry.Dir, gameDirectory))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(entry.MetaDir) &&
            !ArePathsEqual(entry.MetaDir, egstoreDir))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(entry.ManifestPath) &&
            !ArePathsEqual(Path.GetDirectoryName(entry.ManifestPath), egstoreDir))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(entry.PendingManifestPath) &&
            !ArePathsEqual(Path.GetDirectoryName(entry.PendingManifestPath), pendingDir))
        {
            return false;
        }

        return true;
    }

    private bool IsLauncherInstalledDatAligned(string appName, string gameDirectory)
    {
        if (!File.Exists(_launcherInstalledFilePath))
        {
            return false;
        }

        try
        {
            var node = JsonNode.Parse(File.ReadAllText(_launcherInstalledFilePath)) as JsonObject;
            if (node?["InstallationList"] is not JsonArray list)
            {
                return false;
            }

            foreach (var element in list)
            {
                if (element is JsonObject entry &&
                    string.Equals(entry["AppName"]?.GetValue<string>(), appName, StringComparison.OrdinalIgnoreCase))
                {
                    var loc = entry["InstallLocation"]?.GetValue<string>();
                    return !string.IsNullOrWhiteSpace(loc) && ArePathsEqual(loc, gameDirectory);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed to inspect LauncherInstalled.dat: {ex.Message}");
        }

        return false;
    }

    private readonly record struct ItemManifestEntry(
        string FilePath,
        string AppName,
        string DisplayName,
        string InstallLocation,
        string? ManifestLocation,
        string? CompleteManifestPath,
        string? PendingManifestPath,
        string? InstallationGuid,
        bool IsPendingFolder,
        bool IsIncompleteInstall);

    private readonly record struct EoshItemEntry(
        string FilePath,
        string InstallationId,
        string ArtifactId,
        string State,
        string? Dir,
        string? MetaDir,
        string? ManifestPath,
        string? PendingManifestPath);

    private readonly record struct FileByteSnapshot(
        string FilePath,
        bool ExistedBefore,
        byte[]? OriginalBytes);

    private List<ItemManifestEntry> FindCandidateItemManifests(
        EditionSpec edition,
        string? epicAppIdOverride)
    {
        var candidates = new List<ItemManifestEntry>();
        if (!Directory.Exists(_epicManifestsDirectory))
        {
            return candidates;
        }

        foreach (var itemFile in Directory.EnumerateFiles(_epicManifestsDirectory, "*.item", SearchOption.AllDirectories))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(itemFile));
                var root = doc.RootElement;

                var appName = root.TryGetProperty("AppName", out var appProp) ? appProp.GetString()?.Trim() : null;
                var displayName = root.TryGetProperty("DisplayName", out var dispProp) ? dispProp.GetString()?.Trim() ?? string.Empty : string.Empty;
                var installLoc = root.TryGetProperty("InstallLocation", out var locProp) ? locProp.GetString()?.Trim() : null;
                var manifestLoc = root.TryGetProperty("ManifestLocation", out var manProp) ? manProp.GetString()?.Trim() : null;
                var completePath = root.TryGetProperty("CompleteManifestPath", out var compProp) ? compProp.GetString()?.Trim() : null;
                var pendingPath = root.TryGetProperty("PendingManifestPath", out var pendProp) ? pendProp.GetString()?.Trim() : null;
                var installGuid = root.TryGetProperty("InstallationGuid", out var guidProp) ? guidProp.GetString()?.Trim() : null;
                if (string.IsNullOrWhiteSpace(installGuid))
                {
                    installGuid = Path.GetFileNameWithoutExtension(itemFile);
                }

                var isIncomplete = root.TryGetProperty("bIsIncompleteInstall", out var incProp) &&
                                   incProp.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                                   incProp.GetBoolean();
                var isPendingFolder = IsUnderPendingFolder(itemFile);

                if (string.IsNullOrWhiteSpace(appName) || string.IsNullOrWhiteSpace(installLoc))
                {
                    continue;
                }

                if (IsManifestForEdition(edition, appName, displayName, epicAppIdOverride))
                {
                    candidates.Add(new ItemManifestEntry(
                        itemFile,
                        appName,
                        displayName,
                        installLoc,
                        manifestLoc,
                        completePath,
                        pendingPath,
                        installGuid,
                        isPendingFolder,
                        isIncomplete));
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"Failed to parse Epic manifest '{itemFile}': {ex.Message}");
            }
        }

        return candidates;
    }

    private static ItemManifestEntry? SelectPrimaryManifest(
        IReadOnlyList<ItemManifestEntry> candidates,
        string? gameDirectory)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        var ordered = candidates
            .OrderBy(c => c.IsPendingFolder ? 1 : 0)
            .ThenBy(c => c.IsIncompleteInstall ? 1 : 0)
            .ThenBy(c => !string.IsNullOrWhiteSpace(gameDirectory) && ArePathsEqual(c.InstallLocation, gameDirectory) ? 0 : 1)
            .ThenBy(c => HasEgstoreDirectory(c.InstallLocation) ? 0 : 1)
            .ThenBy(c => c.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return ordered[0];
    }

    private List<EoshItemEntry> FindEoshEntries(string appName, string? installationGuid)
    {
        var results = new List<EoshItemEntry>();
        if (!Directory.Exists(_eoshInstalledItemsDirectory))
        {
            return results;
        }

        foreach (var egiFile in Directory.EnumerateFiles(_eoshInstalledItemsDirectory, "*.egi", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(egiFile));
                if (!doc.RootElement.TryGetProperty("v4", out var v4) || v4.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var instId = v4.TryGetProperty("installationId", out var idProp) ? idProp.GetString()?.Trim() ?? string.Empty : string.Empty;
                if (string.IsNullOrWhiteSpace(instId))
                {
                    instId = Path.GetFileNameWithoutExtension(egiFile);
                }

                var artifactId = v4.TryGetProperty("artifactId", out var artProp) ? artProp.GetString()?.Trim() ?? string.Empty : string.Empty;
                var state = v4.TryGetProperty("state", out var stateProp) ? stateProp.GetString()?.Trim() ?? string.Empty : string.Empty;
                var dir = v4.TryGetProperty("dir", out var dirProp) ? dirProp.GetString()?.Trim() : null;
                var metaDir = v4.TryGetProperty("metaDir", out var metaProp) ? metaProp.GetString()?.Trim() : null;
                var manifestPath = v4.TryGetProperty("manifestPath", out var manProp) ? manProp.GetString()?.Trim() : null;
                var pendingPath = v4.TryGetProperty("pendingManifestPath", out var pendProp) ? pendProp.GetString()?.Trim() : null;

                var matchesArtifact = !string.IsNullOrWhiteSpace(artifactId) &&
                                      string.Equals(artifactId, appName, StringComparison.OrdinalIgnoreCase);
                var matchesGuid = !string.IsNullOrWhiteSpace(installationGuid) &&
                                  string.Equals(instId, installationGuid, StringComparison.OrdinalIgnoreCase);

                if (matchesArtifact || matchesGuid)
                {
                    results.Add(new EoshItemEntry(
                        egiFile,
                        instId,
                        artifactId,
                        state,
                        dir,
                        metaDir,
                        manifestPath,
                        pendingPath));
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"Failed to parse EOSH item '{egiFile}': {ex.Message}");
            }
        }

        return results;
    }

    private static EoshItemEntry? SelectPrimaryEoshEntry(
        IReadOnlyList<EoshItemEntry> entries,
        string? primaryInstallationGuid,
        string? gameDirectory)
    {
        if (entries.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(primaryInstallationGuid))
        {
            var exactGuid = entries.FirstOrDefault(e =>
                string.Equals(e.InstallationId, primaryInstallationGuid, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(exactGuid.FilePath))
            {
                return exactGuid;
            }
        }

        return entries
            .OrderBy(e => string.Equals(e.State, "Incomplete", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenBy(e => !string.IsNullOrWhiteSpace(gameDirectory) && ArePathsEqual(e.Dir, gameDirectory) ? 0 : 1)
            .ThenBy(e => e.FilePath, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    private static string? ResolveSourceEgstoreParent(
        string? preferredSourceFolder,
        ItemManifestEntry? primaryCandidate,
        IReadOnlyList<EoshItemEntry> eoshEntries,
        string gameDirectory)
    {
        var possibleDirs = new List<string?>
        {
            preferredSourceFolder,
            primaryCandidate?.InstallLocation
        };
        possibleDirs.AddRange(eoshEntries.Select(e => e.Dir));

        foreach (var dir in possibleDirs)
        {
            if (!string.IsNullOrWhiteSpace(dir) &&
                !ArePathsEqual(dir, gameDirectory) &&
                HasEgstoreDirectory(dir))
            {
                return dir;
            }
        }

        return preferredSourceFolder;
    }

    private static bool IsUnderPendingFolder(string filePath)
    {
        var normalized = filePath.Replace('/', '\\');
        return normalized.Contains(@"\Pending\", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBpsRelativePath(string relativePath)
    {
        var normalized = relativePath.Replace('/', '\\').TrimStart('\\');
        return normalized.Equals("bps", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith(@"bps\", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsManifestForEdition(
        EditionSpec edition,
        string appName,
        string displayName,
        string? epicAppIdOverride)
    {
        if (!string.IsNullOrWhiteSpace(epicAppIdOverride) &&
            string.Equals(appName, epicAppIdOverride.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(edition.DefaultEpicAppId) &&
            string.Equals(appName, edition.DefaultEpicAppId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (edition.Edition == GameEdition.Enhanced &&
            string.Equals(appName, DefaultEnhancedEpicAppId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var isEnhancedName = displayName.Contains("Grand Theft Auto V Enhanced", StringComparison.OrdinalIgnoreCase) ||
                             displayName.Contains("GTA V Enhanced", StringComparison.OrdinalIgnoreCase);
        var isLegacyName = !isEnhancedName &&
                           (displayName.Contains("Grand Theft Auto V", StringComparison.OrdinalIgnoreCase) ||
                            displayName.Contains("GTA V", StringComparison.OrdinalIgnoreCase));

        return (edition.Edition == GameEdition.Enhanced && isEnhancedName) ||
               (edition.Edition == GameEdition.Legacy && isLegacyName);
    }

    private static bool ArePathsEqual(string? pathA, string? pathB)
    {
        if (string.IsNullOrWhiteSpace(pathA) || string.IsNullOrWhiteSpace(pathB))
        {
            return false;
        }

        try
        {
            var normA = Path.GetFullPath(pathA.Replace('/', '\\')).TrimEnd('\\', '/');
            var normB = Path.GetFullPath(pathB.Replace('/', '\\')).TrimEnd('\\', '/');
            return string.Equals(normA, normB, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(
                pathA.Replace('/', '\\').TrimEnd('\\', '/'),
                pathB.Replace('/', '\\').TrimEnd('\\', '/'),
                StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void CaptureFileSnapshot(List<FileByteSnapshot> snapshots, string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) ||
            snapshots.Any(s => ArePathsEqual(s.FilePath, filePath)))
        {
            return;
        }

        if (File.Exists(filePath))
        {
            snapshots.Add(new FileByteSnapshot(filePath, ExistedBefore: true, File.ReadAllBytes(filePath)));
        }
        else
        {
            snapshots.Add(new FileByteSnapshot(filePath, ExistedBefore: false, OriginalBytes: null));
        }
    }

    private static async Task CopyFileAsync(string source, string destination, CancellationToken cancellationToken)
    {
        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
    }

    private void RollbackSnapshots(
        IReadOnlyList<FileByteSnapshot> snapshots,
        IReadOnlyList<string> copiedEgstoreFiles,
        string targetEgstoreDir,
        bool targetEgstoreExistedBefore)
    {
        foreach (var file in copiedEgstoreFiles)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // Ignore cleanup failure.
            }
        }

        if (!targetEgstoreExistedBefore && Directory.Exists(targetEgstoreDir))
        {
            try
            {
                Directory.Delete(targetEgstoreDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup failure.
            }
        }

        foreach (var snap in snapshots)
        {
            try
            {
                if (snap.ExistedBefore && snap.OriginalBytes is not null)
                {
                    var parent = Path.GetDirectoryName(snap.FilePath);
                    if (!string.IsNullOrWhiteSpace(parent))
                    {
                        Directory.CreateDirectory(parent);
                    }

                    File.WriteAllBytes(snap.FilePath, snap.OriginalBytes);
                }
                else if (!snap.ExistedBefore && File.Exists(snap.FilePath))
                {
                    File.Delete(snap.FilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Rollback failed to restore '{snap.FilePath}'.", ex);
            }
        }
    }
}
