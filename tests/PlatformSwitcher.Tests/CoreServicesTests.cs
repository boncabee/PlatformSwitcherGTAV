using Grand_Theft_Auto_V_Platform_Switcher.Models;
using Grand_Theft_Auto_V_Platform_Switcher.Services;
using Xunit;

namespace PlatformSwitcher.Tests;

public sealed class CoreServicesTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly AppLogger _logger;

    public CoreServicesTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"PSGTAV_Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
        _logger = new AppLogger(Path.Combine(_tempRoot, "logs"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch
        {
            // Ignore temp cleanup failure.
        }
    }

    [Fact]
    public async Task SwitchAsync_CopiesFilesAndTogglesSteamAndEpicDlls()
    {
        var gameDir = CreateMockGameDir(EditionSpec.Enhanced);
        File.WriteAllText(Path.Combine(gameDir, "steam_api64.dll"), "STEAM_DLL");

        // Populate RGL and EGL profiles
        var rglProfile = Path.Combine(gameDir, "Switcher", "Enhanced", "RGL");
        Directory.CreateDirectory(rglProfile);
        File.WriteAllText(Path.Combine(rglProfile, "GTA5_Enhanced.exe"), "RGL_EXE");
        File.WriteAllText(Path.Combine(rglProfile, "title.rgl"), "RGL_TITLE");

        var eglProfile = Path.Combine(gameDir, "Switcher", "Enhanced", "EGL");
        Directory.CreateDirectory(eglProfile);
        File.WriteAllText(Path.Combine(eglProfile, "GTA5_Enhanced.exe"), "EGL_EXE");
        File.WriteAllText(Path.Combine(eglProfile, "EOSSDK-Win64-Shipping.dll"), "EPIC_EOS");

        var steamProfile = Path.Combine(gameDir, "Switcher", "Enhanced", "Steam");
        Directory.CreateDirectory(steamProfile);
        File.WriteAllText(Path.Combine(steamProfile, "GTA5_Enhanced.exe"), "STEAM_EXE");

        var detector = new EditionDetector(_logger, appBaseDirectory: _tempRoot, epicManifestsDirectory: Path.Combine(_tempRoot, "empty_epic"));
        var switcher = new PlatformSwitchService(detector, new ProcessGuard(), _logger);

        // 1. Steam -> RGL: steam_api64.dll parked as .steam
        var rglResult = await switcher.SwitchAsync(EditionSpec.Enhanced, gameDir, GamePlatform.RGL);
        Assert.True(rglResult.Success, rglResult.Message);
        Assert.Equal("RGL_EXE", File.ReadAllText(Path.Combine(gameDir, "GTA5_Enhanced.exe")));
        Assert.False(File.Exists(Path.Combine(gameDir, "steam_api64.dll")));
        Assert.True(File.Exists(Path.Combine(gameDir, "steam_api64.dll.steam")));

        // 2. RGL -> EGL: EOSSDK copied
        var eglResult = await switcher.SwitchAsync(EditionSpec.Enhanced, gameDir, GamePlatform.EGL);
        Assert.True(eglResult.Success, eglResult.Message);
        Assert.Equal("EGL_EXE", File.ReadAllText(Path.Combine(gameDir, "GTA5_Enhanced.exe")));
        Assert.True(File.Exists(Path.Combine(gameDir, "EOSSDK-Win64-Shipping.dll")));

        // 3. EGL -> Steam: steam_api64.dll restored, EOSSDK parked as .epic
        var steamResult = await switcher.SwitchAsync(EditionSpec.Enhanced, gameDir, GamePlatform.Steam);
        Assert.True(steamResult.Success, steamResult.Message);
        Assert.Equal("STEAM_EXE", File.ReadAllText(Path.Combine(gameDir, "GTA5_Enhanced.exe")));
        Assert.True(File.Exists(Path.Combine(gameDir, "steam_api64.dll")));
        Assert.False(File.Exists(Path.Combine(gameDir, "steam_api64.dll.steam")));
        Assert.False(File.Exists(Path.Combine(gameDir, "EOSSDK-Win64-Shipping.dll")));
        Assert.True(File.Exists(Path.Combine(gameDir, "EOSSDK-Win64-Shipping.dll.epic")));
    }

    [Fact]
    public async Task SwitchAsync_RollsBackOnMidSwitchFailure()
    {
        var gameDir = CreateMockGameDir(EditionSpec.Legacy);
        File.WriteAllText(Path.Combine(gameDir, "GTA5.exe"), "ORIGINAL_EXE");
        File.WriteAllText(Path.Combine(gameDir, "title.rgl"), "ORIGINAL_RGL");

        var profile = Path.Combine(gameDir, "Switcher", "Legacy", "Steam");
        Directory.CreateDirectory(profile);
        File.WriteAllText(Path.Combine(profile, "GTA5.exe"), "NEW_STEAM_EXE");
        File.WriteAllText(Path.Combine(profile, "title.rgl"), "NEW_STEAM_RGL");

        var detector = new EditionDetector(_logger, appBaseDirectory: _tempRoot, epicManifestsDirectory: Path.Combine(_tempRoot, "empty_epic"));
        var switcher = new PlatformSwitchService(detector, new ProcessGuard(), _logger);

        var result = await switcher.SwitchAsync(
            EditionSpec.Legacy,
            gameDir,
            GamePlatform.Steam,
            testFailAfterCopyFile: _ => throw new IOException("Simulated disk failure"));

        Assert.False(result.Success);
        Assert.Equal("ORIGINAL_EXE", File.ReadAllText(Path.Combine(gameDir, "GTA5.exe")));
        Assert.Equal("ORIGINAL_RGL", File.ReadAllText(Path.Combine(gameDir, "title.rgl")));
    }

    [Fact]
    public async Task BackupCurrentPlatformAsync_CreatesEditionProfileFiles()
    {
        var gameDir = CreateMockGameDir(EditionSpec.Enhanced);
        File.WriteAllText(Path.Combine(gameDir, "GTA5_Enhanced_BE.exe"), "BE_BIN");
        File.WriteAllText(Path.Combine(gameDir, "PlayGTAV.exe"), "PLAY_BIN");
        File.WriteAllText(Path.Combine(gameDir, "title.rgl"), "TITLE_BIN");

        var detector = new EditionDetector(_logger, appBaseDirectory: _tempRoot);
        var switcher = new PlatformSwitchService(detector, new ProcessGuard(), _logger);

        var res = await switcher.BackupCurrentPlatformAsync(EditionSpec.Enhanced, gameDir, GamePlatform.RGL);
        Assert.True(res.Success, res.Message);

        // Portable layout: backups are stored next to the application executable.
        var backupDir = Path.Combine(_tempRoot, "Switcher", "Enhanced", "RGL");
        Assert.True(File.Exists(Path.Combine(backupDir, "GTA5_Enhanced.exe")));
        Assert.True(File.Exists(Path.Combine(backupDir, "GTA5_Enhanced_BE.exe")));
        Assert.True(File.Exists(Path.Combine(backupDir, "PlayGTAV.exe")));
        Assert.True(File.Exists(Path.Combine(backupDir, "title.rgl")));
    }

    [Theory]
    [InlineData("1.0.3411.0", "1.0.3258.0", true)]
    [InlineData("1.0.3411.0", "1.0.3411.0", false)]
    [InlineData("1.0.3258.0", "1.0.3411.0", false)]
    [InlineData("Unknown", "1.0.3411.0", false)]
    public void IsProfileOlderThanActive_DetectsOutdatedProfiles(string active, string profile, bool expected)
    {
        Assert.Equal(expected, PlatformSwitchService.IsProfileOlderThanActive(active, profile));
    }

    [Fact]
    public void EditionDetector_ParsesSteamVdfAndAcfAndLegacyFallback()
    {
        var vdf = """
            "libraryfolders"
            {
                "0"
                {
                    "path"      "C:\\Program Files (x86)\\Steam"
                }
                "1"
                {
                    "path"      "D:\\SteamLibrary"
                }
            }
            """;

        var libs = EditionDetector.ParseSteamLibraryFolders(vdf);
        Assert.Equal(2, libs.Count);
        Assert.Contains(@"D:\SteamLibrary", libs);

        var acf = """
            "AppState"
            {
                "appid"     "3240220"
                "installdir"    "Grand Theft Auto V Enhanced"
            }
            """;
        Assert.Equal("Grand Theft Auto V Enhanced", EditionDetector.ParseSteamAppManifestInstallDir(acf));

        // Legacy fallback: Switcher\Steam (old v1.1 layout)
        var gameDir = CreateMockGameDir(EditionSpec.Legacy);
        var oldSteamProfile = Path.Combine(gameDir, "Switcher", "Steam");
        Directory.CreateDirectory(oldSteamProfile);
        File.WriteAllText(Path.Combine(oldSteamProfile, "GTA5.exe"), "OLD_LAYOUT");

        var detector = new EditionDetector(_logger, appBaseDirectory: _tempRoot);
        var resolved = detector.ResolveProfileDirectory(EditionSpec.Legacy, gameDir, GamePlatform.Steam);
        Assert.Equal(oldSteamProfile, resolved);
    }

    [Fact]
    public void EditionDetector_ParsesEpicManifestAndDetectsActivePlatform()
    {
        var manifestsDir = Path.Combine(_tempRoot, "EpicManifests");
        Directory.CreateDirectory(manifestsDir);
        var gameDir = CreateMockGameDir(EditionSpec.Enhanced);
        File.WriteAllText(Path.Combine(gameDir, "EOSSDK-Win64-Shipping.dll"), "EOS");

        var itemJson = $$"""
            {
              "AppName": "EnhancedEpicAppId123",
              "DisplayName": "Grand Theft Auto V Enhanced",
              "InstallLocation": "{{gameDir.Replace("\\", "\\\\")}}"
            }
            """;
        File.WriteAllText(Path.Combine(manifestsDir, "gtav_enhanced.item"), itemJson);

        var detector = new EditionDetector(_logger, appBaseDirectory: _tempRoot, epicManifestsDirectory: manifestsDir);
        var epicApp = detector.FindEpicAppName(EditionSpec.Enhanced, gameDir);
        Assert.Equal("EnhancedEpicAppId123", epicApp);
        Assert.Equal(GamePlatform.EGL, detector.DetectActivePlatform(EditionSpec.Enhanced, gameDir));
    }

    [Fact]
    public void BattlEyeService_InspectsPresenceAndFolderHealth()
    {
        var gameDir = CreateMockGameDir(EditionSpec.Enhanced);
        var service = new BattlEyeService(_logger);

        var missing = service.Inspect(EditionSpec.Enhanced, gameDir);
        Assert.False(missing.IsHealthy);
        Assert.False(missing.IsExecutablePresent);

        File.WriteAllText(Path.Combine(gameDir, "GTA5_Enhanced_BE.exe"), "BE");
        var beFolder = Path.Combine(gameDir, "BattlEye");
        Directory.CreateDirectory(beFolder);
        File.WriteAllText(Path.Combine(beFolder, "BEService_x64.exe"), "SVC");

        var present = service.Inspect(EditionSpec.Enhanced, gameDir);
        Assert.True(present.IsHealthy);
        Assert.True(present.IsExecutablePresent);
        Assert.True(present.HasCoreFiles);
    }

    [Fact]
    public void SettingsService_SavesLoadsAndRecoversFromCorruption()
    {
        var settingsDir = Path.Combine(_tempRoot, "settings");
        var svc = new SettingsService(_logger, settingsDir);

        var original = new AppSettings
        {
            SelectedEdition = GameEdition.Enhanced,
            DisableBattlEyeOnLaunch = true,
            UseDarkTheme = false
        };
        original.Enhanced.GameDirectory = @"D:\Games\GTAVEnhanced";
        original.Enhanced.CurrentPlatform = GamePlatform.Steam;

        svc.Save(original);
        var loaded = svc.Load();

        Assert.Equal(GameEdition.Enhanced, loaded.SelectedEdition);
        Assert.True(loaded.DisableBattlEyeOnLaunch);
        Assert.False(loaded.UseDarkTheme);
        Assert.Equal(@"D:\Games\GTAVEnhanced", loaded.Enhanced.GameDirectory);
        Assert.Equal(GamePlatform.Steam, loaded.Enhanced.CurrentPlatform);

        // Corrupt the settings file and verify graceful fallback + backup creation
        File.WriteAllText(svc.SettingsFilePath, "{ broken json");
        var recovered = svc.Load();
        Assert.Equal(GameEdition.Legacy, recovered.SelectedEdition);
        Assert.NotEmpty(Directory.GetFiles(settingsDir, "settings.corrupted.*.json"));
    }

    [Fact]
    public void GameLauncherAndCli_BuildCorrectUrisAndParseFlags()
    {
        var detector = new EditionDetector(_logger, appBaseDirectory: _tempRoot, epicManifestsDirectory: Path.Combine(_tempRoot, "none"));
        var launcher = new GameLauncherService(detector, _logger);

        var steamLegacy = launcher.BuildLaunchPlan(EditionSpec.Legacy, null, GamePlatform.Steam, disableBattlEye: false);
        Assert.True(steamLegacy.IsValid);
        Assert.Equal("steam://rungameid/271590", steamLegacy.FileName);

        var steamEnhancedNoBe = launcher.BuildLaunchPlan(EditionSpec.Enhanced, null, GamePlatform.Steam, disableBattlEye: true);
        Assert.True(steamEnhancedNoBe.IsValid);
        Assert.Equal("steam://rungameid/3240220//-nobattleye", steamEnhancedNoBe.FileName);

        var epicLegacy = launcher.BuildLaunchPlan(EditionSpec.Legacy, null, GamePlatform.EGL, disableBattlEye: false);
        Assert.True(epicLegacy.IsValid);
        Assert.Contains("9d2d0eb64d5c44529cece33fe2a46482", epicLegacy.FileName);

        var cli = CliRunner.ParseArguments(["--edition", "enhanced", "--switch", "steam", "--launch", "--nobattleye"]);
        Assert.True(cli.HasCliCommand);
        Assert.Null(cli.Error);
        Assert.Equal(GameEdition.Enhanced, cli.Edition);
        Assert.Equal(GamePlatform.Steam, cli.SwitchTo);
        Assert.True(cli.Launch);
        Assert.True(cli.DisableBattlEye);
    }

    [Fact]
    public async Task DoctorService_DetectsConflictsAppliesFixesAndBuildsMarkdownReport()
    {
        var gameDir = CreateMockGameDir(EditionSpec.Legacy);
        File.WriteAllText(Path.Combine(gameDir, "steam_api64.dll"), "ORIG");
        File.WriteAllText(Path.Combine(gameDir, "steam_api64.dll.steam"), "BACK");

        var detector = new EditionDetector(_logger, appBaseDirectory: _tempRoot, epicManifestsDirectory: Path.Combine(_tempRoot, "none"));
        var guard = new ProcessGuard();
        var switcher = new PlatformSwitchService(detector, guard, _logger);
        var beSvc = new BattlEyeService(_logger);
        var regSvc = new RegistryService(_logger);
        var doctor = new DoctorService(detector, switcher, beSvc, regSvc, guard);

        var edSettings = new EditionSettings
        {
            GameDirectory = gameDir,
            CurrentPlatform = GamePlatform.Steam
        };

        var report = doctor.Diagnose(EditionSpec.Legacy, edSettings);
        var dllIssue = report.Checks.FirstOrDefault(c => c.Id == "steam-api-dll");
        Assert.NotNull(dllIssue);
        Assert.Equal(DoctorSeverity.Warning, dllIssue!.Severity);
        Assert.Equal(DoctorFixType.RepairSteamApiConflict, dllIssue.FixType);

        var fixRes = await doctor.ApplyFixAsync(dllIssue, EditionSpec.Legacy, edSettings);
        Assert.True(fixRes.Success, fixRes.Message);
        Assert.True(File.Exists(Path.Combine(gameDir, "steam_api64.dll")));
        Assert.False(File.Exists(Path.Combine(gameDir, "steam_api64.dll.steam")));

        var md = doctor.BuildMarkdownReport(
            report,
            switcher.GetVersionSnapshot(EditionSpec.Legacy, gameDir),
            beSvc.Inspect(EditionSpec.Legacy, gameDir));
        Assert.Contains("## Platform Switcher GTA V - Diagnostic Report", md);
        Assert.Contains("### Checks", md);
    }

    [Theory]
    [InlineData("2.0.0", "2.1.0", true)]
    [InlineData("2.0.0", "2.0.0", false)]
    [InlineData("2.1.0", "2.0.9", false)]
    public void UpdateService_ComparesSemanticVersions(string current, string latest, bool expected)
    {
        Assert.Equal(expected, UpdateService.IsNewerVersion(current, latest));
    }

    [Fact]
    public void EditionDetector_DetectActivePlatform_DoesNotMisclassifyRglWhenEgStoreExists()
    {
        var detector = new EditionDetector(_logger, appBaseDirectory: _tempRoot, epicManifestsDirectory: Path.Combine(_tempRoot, "none"));

        var rglDir = CreateMockGameDir(EditionSpec.Enhanced);
        Directory.CreateDirectory(Path.Combine(rglDir, ".egstore"));
        File.WriteAllText(Path.Combine(rglDir, "title.rgl"), "RGL");
        File.WriteAllText(Path.Combine(rglDir, "EOSSDK-Win64-Shipping.dll.epic"), "PARKED_EOS");
        Assert.Equal(GamePlatform.RGL, detector.DetectActivePlatform(EditionSpec.Enhanced, rglDir));

        var eglDir = CreateMockGameDir(EditionSpec.Enhanced);
        Directory.CreateDirectory(Path.Combine(eglDir, ".egstore"));
        File.WriteAllText(Path.Combine(eglDir, "EOSSDK-Win64-Shipping.dll"), "ACTIVE_EOS");
        Assert.Equal(GamePlatform.EGL, detector.DetectActivePlatform(EditionSpec.Enhanced, eglDir));

        var fallbackEglDir = CreateMockGameDir(EditionSpec.Enhanced);
        Directory.CreateDirectory(Path.Combine(fallbackEglDir, ".egstore"));
        Assert.Equal(GamePlatform.EGL, detector.DetectActivePlatform(EditionSpec.Enhanced, fallbackEglDir));
    }

    [Fact]
    public async Task SwitchAsync_RollsBackSteamAndEpicDlls_WhenFailureOccursDuringDllUpdate()
    {
        var gameDir = CreateMockGameDir(EditionSpec.Enhanced);
        File.WriteAllText(Path.Combine(gameDir, "GTA5_Enhanced.exe"), "STEAM_EXE");
        File.WriteAllText(Path.Combine(gameDir, "steam_api64.dll"), "STEAM_DLL");
        File.WriteAllText(Path.Combine(gameDir, "EOSSDK-Win64-Shipping.dll.epic"), "PARKED_EOS");

        var eglProfile = Path.Combine(gameDir, "Switcher", "Enhanced", "EGL");
        Directory.CreateDirectory(eglProfile);
        File.WriteAllText(Path.Combine(eglProfile, "GTA5_Enhanced.exe"), "EGL_EXE");

        var detector = new EditionDetector(_logger, appBaseDirectory: _tempRoot, epicManifestsDirectory: Path.Combine(_tempRoot, "none"));
        var switcher = new PlatformSwitchService(detector, new ProcessGuard(), _logger);

        var result = await switcher.SwitchAsync(
            EditionSpec.Enhanced,
            gameDir,
            GamePlatform.EGL,
            testFailAfterSteamDllUpdate: () => throw new IOException("Simulated DLL update failure"));

        Assert.False(result.Success);
        Assert.Equal("STEAM_EXE", File.ReadAllText(Path.Combine(gameDir, "GTA5_Enhanced.exe")));
        Assert.True(File.Exists(Path.Combine(gameDir, "steam_api64.dll")));
        Assert.Equal("STEAM_DLL", File.ReadAllText(Path.Combine(gameDir, "steam_api64.dll")));
        Assert.False(File.Exists(Path.Combine(gameDir, "steam_api64.dll.steam")));
        Assert.True(File.Exists(Path.Combine(gameDir, "EOSSDK-Win64-Shipping.dll.epic")));
    }

    [Fact]
    public async Task BackupCurrentPlatformAsync_PreservesExistingProfile_OnMidBackupFailure()
    {
        var gameDir = CreateMockGameDir(EditionSpec.Enhanced);
        File.WriteAllText(Path.Combine(gameDir, "GTA5_Enhanced.exe"), "NEW_EXE");
        File.WriteAllText(Path.Combine(gameDir, "title.rgl"), "NEW_TITLE");

        var profileDir = Path.Combine(gameDir, "Switcher", "Enhanced", "RGL");
        Directory.CreateDirectory(profileDir);
        File.WriteAllText(Path.Combine(profileDir, "GTA5_Enhanced.exe"), "OLD_EXE");
        File.WriteAllText(Path.Combine(profileDir, "title.rgl"), "OLD_TITLE");

        var detector = new EditionDetector(_logger, appBaseDirectory: _tempRoot, epicManifestsDirectory: Path.Combine(_tempRoot, "none"));
        var switcher = new PlatformSwitchService(detector, new ProcessGuard(), _logger);

        var copyCount = 0;
        var res = await switcher.BackupCurrentPlatformAsync(
            EditionSpec.Enhanced,
            gameDir,
            GamePlatform.RGL,
            testFailAfterCopyFile: _ =>
            {
                copyCount++;
                if (copyCount >= 2)
                {
                    throw new IOException("Simulated backup copy failure");
                }
            });

        Assert.False(res.Success);
        Assert.Equal("OLD_EXE", File.ReadAllText(Path.Combine(profileDir, "GTA5_Enhanced.exe")));
        Assert.Equal("OLD_TITLE", File.ReadAllText(Path.Combine(profileDir, "title.rgl")));

        var editionSwitcherDir = Path.Combine(gameDir, "Switcher", "Enhanced");
        Assert.Empty(Directory.GetDirectories(editionSwitcherDir, ".RGL_staging_*"));
    }

    [Fact]
    public void DoctorService_Diagnose_DetectsRglRegistryMismatchWhenUnconfigured()
    {
        var gameDir = CreateMockGameDir(EditionSpec.Legacy);
        File.WriteAllText(Path.Combine(gameDir, "title.rgl"), "RGL");

        var detector = new EditionDetector(_logger, appBaseDirectory: _tempRoot, epicManifestsDirectory: Path.Combine(_tempRoot, "none"));
        var guard = new ProcessGuard();
        var switcher = new PlatformSwitchService(detector, guard, _logger);
        var beSvc = new BattlEyeService(_logger);
        var regSvc = new RegistryService(_logger);
        var doctor = new DoctorService(detector, switcher, beSvc, regSvc, guard);

        var edSettings = new EditionSettings
        {
            GameDirectory = gameDir,
            CurrentPlatform = GamePlatform.RGL
        };

        var report = doctor.Diagnose(EditionSpec.Legacy, edSettings);
        var regCheck = report.Checks.FirstOrDefault(c => c.Id == "registry-sync");
        Assert.NotNull(regCheck);
        Assert.Contains("InstallFolder=", regCheck!.Details);
    }

    [Fact]
    public async Task BackupCurrentPlatformAsync_RejectsMismatchedPlatformSignatures()
    {
        var gameDir = CreateMockGameDir(EditionSpec.Legacy);
        File.WriteAllText(Path.Combine(gameDir, "steam_api64.dll"), "STEAM_API");

        var detector = new EditionDetector(_logger, appBaseDirectory: _tempRoot, epicManifestsDirectory: Path.Combine(_tempRoot, "none"));
        var switcher = new PlatformSwitchService(detector, new ProcessGuard(_ => false), _logger);

        // Steam active folder cannot be backed up into Epic Games or Rockstar Games slots
        var eglAttempt = await switcher.BackupCurrentPlatformAsync(EditionSpec.Legacy, gameDir, GamePlatform.EGL);
        Assert.False(eglAttempt.Success);
        Assert.Contains("EOSSDK-Win64-Shipping.dll", eglAttempt.Message);
        Assert.False(Directory.Exists(Path.Combine(_tempRoot, "Switcher", "Legacy", "EGL")));

        var rglAttempt = await switcher.BackupCurrentPlatformAsync(EditionSpec.Legacy, gameDir, GamePlatform.RGL);
        Assert.False(rglAttempt.Success);
        Assert.Contains("steam_api64.dll", rglAttempt.Message);

        // Backing up into Steam slot succeeds
        var steamAttempt = await switcher.BackupCurrentPlatformAsync(EditionSpec.Legacy, gameDir, GamePlatform.Steam);
        Assert.True(steamAttempt.Success, steamAttempt.Message);
        Assert.True(File.Exists(Path.Combine(_tempRoot, "Switcher", "Legacy", "Steam", "GTA5.exe")));
    }

    [Fact]
    public async Task GetVersionSnapshot_FlagsInvalidEglProfile_BlocksSwitch_And_CleanInvalidProfileRemovesIt()
    {
        var gameDir = CreateMockGameDir(EditionSpec.Legacy);
        var detector = new EditionDetector(_logger, appBaseDirectory: _tempRoot, epicManifestsDirectory: Path.Combine(_tempRoot, "none"));
        var switcher = new PlatformSwitchService(detector, new ProcessGuard(_ => false), _logger);

        // Simulate polluted EGL profile containing GTA5.exe without EOSSDK-Win64-Shipping.dll
        var invalidEglDir = Path.Combine(_tempRoot, "Switcher", "Legacy", "EGL");
        Directory.CreateDirectory(invalidEglDir);
        File.WriteAllText(Path.Combine(invalidEglDir, "GTA5.exe"), "POLLUTED_STEAM_EXE");

        var snapshot = switcher.GetVersionSnapshot(EditionSpec.Legacy, gameDir);
        Assert.False(snapshot.HasEglProfile);
        Assert.True(snapshot.IsEglProfileInvalid);
        Assert.Equal("Invalid", snapshot.EglProfileVersion);

        var switchAttempt = await switcher.SwitchAsync(EditionSpec.Legacy, gameDir, GamePlatform.EGL);
        Assert.False(switchAttempt.Success);
        Assert.Contains("EOSSDK-Win64-Shipping.dll", switchAttempt.Message);

        var cleanRes = switcher.CleanInvalidProfile(EditionSpec.Legacy, gameDir, GamePlatform.EGL);
        Assert.True(cleanRes.Success, cleanRes.Message);
        Assert.False(Directory.Exists(invalidEglDir));

        var afterClean = switcher.GetVersionSnapshot(EditionSpec.Legacy, gameDir);
        Assert.False(afterClean.HasEglProfile);
        Assert.False(afterClean.IsEglProfileInvalid);
        Assert.Equal("Not set", afterClean.EglProfileVersion);
    }

    [Fact]
    public async Task EpicManifestService_RelocateAndAdoptAsync_CopiesEgstore_AutoBacksUpSteam_And_PreservesJsonFields()
    {
        var gameDir = CreateMockGameDir(EditionSpec.Legacy);
        File.WriteAllText(Path.Combine(gameDir, "PlayGTAV.exe"), "STEAM_PLAY");
        File.WriteAllText(Path.Combine(gameDir, "steam_api64.dll"), "STEAM_API");

        var tempEpicDir = Path.Combine(_tempRoot, "TempEpicInstall");
        var tempEgstore = Path.Combine(tempEpicDir, ".egstore");
        Directory.CreateDirectory(tempEgstore);
        File.WriteAllText(Path.Combine(tempEgstore, "chunk.manifest"), "MANIFEST_BINARY");
        File.WriteAllText(Path.Combine(tempEgstore, "chunk.mancpn"), "MANCPN_DATA");

        var manifestsDir = Path.Combine(_tempRoot, "EpicManifests_Adopt");
        Directory.CreateDirectory(manifestsDir);
        var itemPath = Path.Combine(manifestsDir, "gtav_legacy.item");
        var escapedTempEpic = tempEpicDir.Replace("\\", "\\\\");
        var forwardTempEpic = tempEpicDir.Replace("\\", "/");
        File.WriteAllText(itemPath, $$"""
            {
              "FormatVersion": 0,
              "bIsIncompleteInstall": true,
              "AppName": "9d2d0eb64d5c44529cece33fe2a46482",
              "DisplayName": "Grand Theft Auto V",
              "InstallLocation": "{{escapedTempEpic}}",
              "ManifestLocation": "{{forwardTempEpic}}/.egstore",
              "StagingLocation": "{{forwardTempEpic}}/.egstore/bps",
              "CatalogNamespace": "0584d2013f0149a791e7b9bad0eec102",
              "CatalogItemId": "bee0123456789",
              "CustomEpicField": "PreserveMe100Percent"
            }
            """);

        var launcherDatPath = Path.Combine(_tempRoot, "UnrealEngineLauncher", "LauncherInstalled.dat");
        Directory.CreateDirectory(Path.GetDirectoryName(launcherDatPath)!);
        File.WriteAllText(launcherDatPath, $$"""
            {
              "InstallationList": [
                {
                  "InstallLocation": "C:\\Games\\Fortnite",
                  "AppName": "Fortnite"
                },
                {
                  "InstallLocation": "{{escapedTempEpic}}",
                  "AppName": "9d2d0eb64d5c44529cece33fe2a46482"
                }
              ]
            }
            """);

        var guard = new ProcessGuard(_ => false);
        var detector = new EditionDetector(_logger, appBaseDirectory: _tempRoot, epicManifestsDirectory: manifestsDir);
        var switcher = new PlatformSwitchService(detector, guard, _logger);
        var epicSvc = new EpicManifestService(guard, _logger, manifestsDir, launcherDatPath);

        var beforeInspect = epicSvc.Inspect(EditionSpec.Legacy, gameDir);
        Assert.Equal(EpicManifestState.PointsElsewhere, beforeInspect.State);
        Assert.True(beforeInspect.HasSourceEgstore);
        Assert.False(beforeInspect.HasTargetEgstore);

        var adoptRes = await epicSvc.RelocateAndAdoptAsync(EditionSpec.Legacy, gameDir, switcher);
        Assert.True(adoptRes.Success, adoptRes.Message);

        // 1. Steam profile was automatically backed up before parking steam_api64.dll
        Assert.True(File.Exists(Path.Combine(_tempRoot, "Switcher", "Legacy", "Steam", "GTA5.exe")));
        Assert.False(File.Exists(Path.Combine(gameDir, "steam_api64.dll")));
        Assert.True(File.Exists(Path.Combine(gameDir, "steam_api64.dll.steam")));

        // 2. .egstore files were copied into gameDir
        Assert.True(File.Exists(Path.Combine(gameDir, ".egstore", "chunk.manifest")));
        Assert.True(File.Exists(Path.Combine(gameDir, ".egstore", "chunk.mancpn")));

        // 3. .item manifest paths were updated and custom fields were preserved
        var updatedItemText = File.ReadAllText(itemPath);
        Assert.Contains("PreserveMe100Percent", updatedItemText);
        Assert.Contains($"{gameDir.Replace('\\', '/')}/.egstore", updatedItemText);

        // 4. LauncherInstalled.dat updated GTA V and kept Fortnite untouched
        var updatedDatText = File.ReadAllText(launcherDatPath);
        Assert.Contains("Fortnite", updatedDatText);

        var afterInspect = epicSvc.Inspect(EditionSpec.Legacy, gameDir);
        Assert.Equal(EpicManifestState.Aligned, afterInspect.State);
        Assert.True(afterInspect.HasTargetEgstore);
        Assert.True(afterInspect.IsLauncherInstalledAligned);
    }

    [Fact]
    public async Task EpicManifestService_BlocksWhenEpicRunning_And_RollsBackOnFailure()
    {
        var gameDir = CreateMockGameDir(EditionSpec.Legacy);
        File.WriteAllText(Path.Combine(gameDir, "steam_api64.dll"), "STEAM_API");

        var tempEpicDir = Path.Combine(_tempRoot, "TempEpicRollback");
        Directory.CreateDirectory(Path.Combine(tempEpicDir, ".egstore"));
        File.WriteAllText(Path.Combine(tempEpicDir, ".egstore", "chunk.manifest"), "MANIFEST");

        var manifestsDir = Path.Combine(_tempRoot, "EpicManifests_Rollback");
        Directory.CreateDirectory(manifestsDir);
        var itemPath = Path.Combine(manifestsDir, "gtav.item");
        var originalItemJson = $$"""
            {
              "AppName": "9d2d0eb64d5c44529cece33fe2a46482",
              "DisplayName": "Grand Theft Auto V",
              "InstallLocation": "{{tempEpicDir.Replace("\\", "\\\\")}}"
            }
            """;
        File.WriteAllText(itemPath, originalItemJson);
        var launcherDatPath = Path.Combine(_tempRoot, "LauncherInstalled_Rollback.dat");

        // 1. Blocked when EpicGamesLauncher is running
        var runningGuard = new ProcessGuard(name => name.Equals("EpicGamesLauncher", StringComparison.OrdinalIgnoreCase));
        var detector = new EditionDetector(_logger, appBaseDirectory: _tempRoot, epicManifestsDirectory: manifestsDir);
        var switcher = new PlatformSwitchService(detector, runningGuard, _logger);
        var epicRunningSvc = new EpicManifestService(runningGuard, _logger, manifestsDir, launcherDatPath);

        var blockedRes = await epicRunningSvc.RelocateAndAdoptAsync(EditionSpec.Legacy, gameDir, switcher);
        Assert.False(blockedRes.Success);
        Assert.Contains("currently running", blockedRes.Message);
        Assert.Equal(originalItemJson, File.ReadAllText(itemPath));

        // 2. Rolls back all state when a failure occurs mid-operation
        var idleGuard = new ProcessGuard(_ => false);
        var epicIdleSvc = new EpicManifestService(idleGuard, _logger, manifestsDir, launcherDatPath);
        var failedRes = await epicIdleSvc.RelocateAndAdoptAsync(
            EditionSpec.Legacy,
            gameDir,
            switcher,
            testFailBeforeLauncherInstalledWrite: () => throw new IOException("Simulated write error"));

        Assert.False(failedRes.Success);
        Assert.Equal(originalItemJson, File.ReadAllText(itemPath));
        Assert.False(File.Exists(launcherDatPath));
        Assert.True(File.Exists(Path.Combine(gameDir, "steam_api64.dll")));
        Assert.False(File.Exists(Path.Combine(gameDir, "steam_api64.dll.steam")));
        Assert.False(Directory.Exists(Path.Combine(gameDir, ".egstore")));
    }

    [Fact]
    public async Task DoctorService_DetectsAndFixes_InvalidEglProfile_And_EpicManifestAdoption()
    {
        var gameDir = CreateMockGameDir(EditionSpec.Legacy);
        File.WriteAllText(Path.Combine(gameDir, "steam_api64.dll"), "STEAM_API");

        // Polluted EGL backup
        var invalidEglDir = Path.Combine(_tempRoot, "Switcher", "Legacy", "EGL");
        Directory.CreateDirectory(invalidEglDir);
        File.WriteAllText(Path.Combine(invalidEglDir, "GTA5.exe"), "FAKE_EGL_EXE");

        // Bootstrap Epic folder
        var tempEpicDir = Path.Combine(_tempRoot, "TempEpicDoctor");
        Directory.CreateDirectory(Path.Combine(tempEpicDir, ".egstore"));
        File.WriteAllText(Path.Combine(tempEpicDir, ".egstore", "item.manifest"), "MANIFEST");

        var manifestsDir = Path.Combine(_tempRoot, "EpicManifests_Doctor");
        Directory.CreateDirectory(manifestsDir);
        File.WriteAllText(Path.Combine(manifestsDir, "gtav.item"), $$"""
            {
              "AppName": "9d2d0eb64d5c44529cece33fe2a46482",
              "DisplayName": "Grand Theft Auto V",
              "InstallLocation": "{{tempEpicDir.Replace("\\", "\\\\")}}"
            }
            """);
        var launcherDatPath = Path.Combine(_tempRoot, "LauncherInstalled_Doctor.dat");

        var guard = new ProcessGuard(_ => false);
        var detector = new EditionDetector(_logger, appBaseDirectory: _tempRoot, epicManifestsDirectory: manifestsDir);
        var switcher = new PlatformSwitchService(detector, guard, _logger);
        var beSvc = new BattlEyeService(_logger);
        var regSvc = new RegistryService(_logger);
        var epicSvc = new EpicManifestService(guard, _logger, manifestsDir, launcherDatPath);
        var doctor = new DoctorService(detector, switcher, beSvc, regSvc, guard, epicSvc);

        var edSettings = new EditionSettings
        {
            GameDirectory = gameDir,
            CurrentPlatform = GamePlatform.Steam
        };

        var report = doctor.Diagnose(EditionSpec.Legacy, edSettings);
        var invalidCheck = report.Checks.FirstOrDefault(c => c.Id == "invalid-epic-profile");
        var adoptCheck = report.Checks.FirstOrDefault(c => c.Id == "epic-manifest-sync");

        Assert.NotNull(invalidCheck);
        Assert.Equal(DoctorFixType.CleanInvalidProfile, invalidCheck!.FixType);
        Assert.NotNull(adoptCheck);
        Assert.Equal(DoctorFixType.AdoptEpicInstallation, adoptCheck!.FixType);

        var cleanRes = await doctor.ApplyFixAsync(invalidCheck, EditionSpec.Legacy, edSettings);
        Assert.True(cleanRes.Success, cleanRes.Message);

        var adoptRes = await doctor.ApplyFixAsync(adoptCheck, EditionSpec.Legacy, edSettings);
        Assert.True(adoptRes.Success, adoptRes.Message);

        var reportAfter = doctor.Diagnose(EditionSpec.Legacy, edSettings);
        Assert.Null(reportAfter.Checks.FirstOrDefault(c => c.Id == "invalid-epic-profile"));
        var alignedCheck = reportAfter.Checks.FirstOrDefault(c => c.Id == "epic-manifest-sync");
        Assert.NotNull(alignedCheck);
        Assert.Equal(DoctorSeverity.Ok, alignedCheck!.Severity);
    }

    [Fact]
    public async Task EpicManifestService_RelocateAndAdopt_AutoBacksUpActiveRglProfile()
    {
        var gameDir = CreateMockGameDir(EditionSpec.Legacy);
        File.WriteAllText(Path.Combine(gameDir, "PlayGTAV.exe"), "RGL_PLAY");
        File.WriteAllText(Path.Combine(gameDir, "title.rgl"), "RGL_TITLE");

        var tempEpicDir = Path.Combine(_tempRoot, "TempEpicForRgl");
        Directory.CreateDirectory(Path.Combine(tempEpicDir, ".egstore"));
        File.WriteAllText(Path.Combine(tempEpicDir, ".egstore", "chunk.manifest"), "MANIFEST");

        var manifestsDir = Path.Combine(_tempRoot, "EpicManifests_RglAdopt");
        Directory.CreateDirectory(manifestsDir);
        File.WriteAllText(Path.Combine(manifestsDir, "gtav.item"), $$"""
            {
              "AppName": "9d2d0eb64d5c44529cece33fe2a46482",
              "DisplayName": "Grand Theft Auto V",
              "InstallLocation": "{{tempEpicDir.Replace("\\", "\\\\")}}"
            }
            """);
        var launcherDatPath = Path.Combine(_tempRoot, "LauncherInstalled_RglAdopt.dat");

        var guard = new ProcessGuard(_ => false);
        var detector = new EditionDetector(_logger, appBaseDirectory: _tempRoot, epicManifestsDirectory: manifestsDir);
        var switcher = new PlatformSwitchService(detector, guard, _logger);
        var epicSvc = new EpicManifestService(guard, _logger, manifestsDir, launcherDatPath);

        var res = await epicSvc.RelocateAndAdoptAsync(EditionSpec.Legacy, gameDir, switcher);
        Assert.True(res.Success, res.Message);
        Assert.True(File.Exists(Path.Combine(_tempRoot, "Switcher", "Legacy", "RGL", "GTA5.exe")));
        Assert.True(File.Exists(Path.Combine(_tempRoot, "Switcher", "Legacy", "RGL", "title.rgl")));
    }

    [Fact]
    public void DoctorService_NoFalsePositiveEpicWarning_WhenUserHasNoEpicSetup()
    {
        var gameDir = CreateMockGameDir(EditionSpec.Legacy);
        File.WriteAllText(Path.Combine(gameDir, "steam_api64.dll"), "STEAM_API");

        var steamProfile = Path.Combine(_tempRoot, "Switcher", "Legacy", "Steam");
        Directory.CreateDirectory(steamProfile);
        File.WriteAllText(Path.Combine(steamProfile, "GTA5.exe"), "STEAM_EXE");

        var guard = new ProcessGuard(_ => false);
        var detector = new EditionDetector(_logger, appBaseDirectory: _tempRoot, epicManifestsDirectory: Path.Combine(_tempRoot, "no_epic_manifests"));
        var switcher = new PlatformSwitchService(detector, guard, _logger);
        var beSvc = new BattlEyeService(_logger);
        var regSvc = new RegistryService(_logger);
        var doctor = new DoctorService(detector, switcher, beSvc, regSvc, guard);

        var edSettings = new EditionSettings
        {
            GameDirectory = gameDir,
            CurrentPlatform = GamePlatform.Steam
        };

        var report = doctor.Diagnose(EditionSpec.Legacy, edSettings);
        var epicCheck = report.Checks.FirstOrDefault(c => c.Id == "epic-manifest-sync");
        Assert.NotNull(epicCheck);
        Assert.Equal(DoctorSeverity.Ok, epicCheck!.Severity);
    }

    [Fact]
    public async Task VersionedEpicSdk_DetectedBackedUpSwitchedParkedAndRepaired()
    {
        var gameDir = CreateMockGameDir(EditionSpec.Enhanced);
        const string versionedEos = "EOSSDK-Win64-Shipping-1.17.1.3.dll";
        File.WriteAllText(Path.Combine(gameDir, versionedEos), "ENHANCED_EOS_1_17_1_3");
        File.WriteAllText(Path.Combine(gameDir, "PlayGTAV.exe"), "EPIC_PLAY");
        File.WriteAllText(Path.Combine(gameDir, "steam_api64.dll.steam"), "PARKED_STEAM");

        var steamProfile = Path.Combine(_tempRoot, "Switcher", "Enhanced", "Steam");
        Directory.CreateDirectory(steamProfile);
        File.WriteAllText(Path.Combine(steamProfile, "GTA5_Enhanced.exe"), "STEAM_EXE");

        var guard = new ProcessGuard(_ => false);
        var detector = new EditionDetector(_logger, appBaseDirectory: _tempRoot, epicManifestsDirectory: Path.Combine(_tempRoot, "no_epic"));
        var switcher = new PlatformSwitchService(detector, guard, _logger);

        // 1. Detects Enhanced Epic Games installation via version-suffixed EOS SDK
        Assert.Equal(GamePlatform.EGL, detector.DetectActivePlatform(EditionSpec.Enhanced, gameDir));

        // 2. Backs up version-suffixed EOS SDK into EGL profile and validates profile health
        var backupRes = await switcher.BackupCurrentPlatformAsync(EditionSpec.Enhanced, gameDir, GamePlatform.EGL);
        Assert.True(backupRes.Success, backupRes.Message);
        Assert.True(File.Exists(Path.Combine(_tempRoot, "Switcher", "Enhanced", "EGL", versionedEos)));

        var snapshot = switcher.GetVersionSnapshot(EditionSpec.Enhanced, gameDir);
        Assert.True(snapshot.HasEglProfile);
        Assert.False(snapshot.IsEglProfileInvalid);

        // 3. Switching EGL -> Steam parks EOSSDK-Win64-Shipping-1.17.1.3.dll as .epic and restores steam_api64.dll
        var toSteam = await switcher.SwitchAsync(EditionSpec.Enhanced, gameDir, GamePlatform.Steam);
        Assert.True(toSteam.Success, toSteam.Message);
        Assert.True(File.Exists(Path.Combine(gameDir, "steam_api64.dll")));
        Assert.False(File.Exists(Path.Combine(gameDir, versionedEos)));
        Assert.True(File.Exists(Path.Combine(gameDir, versionedEos + ".epic")));

        // 4. Switching Steam -> EGL restores EOSSDK-Win64-Shipping-1.17.1.3.dll and parks steam_api64.dll
        var toEgl = await switcher.SwitchAsync(EditionSpec.Enhanced, gameDir, GamePlatform.EGL);
        Assert.True(toEgl.Success, toEgl.Message);
        Assert.True(File.Exists(Path.Combine(gameDir, versionedEos)));
        Assert.False(File.Exists(Path.Combine(gameDir, versionedEos + ".epic")));
        Assert.False(File.Exists(Path.Combine(gameDir, "steam_api64.dll")));

        // 5. Doctor detects leftover versioned EOS SDK when Steam is active and parks it cleanly
        File.WriteAllText(Path.Combine(gameDir, "steam_api64.dll"), "ACTIVE_STEAM");
        var doctor = new DoctorService(detector, switcher, new BattlEyeService(_logger), new RegistryService(_logger), guard);
        var edSettings = new EditionSettings { GameDirectory = gameDir, CurrentPlatform = GamePlatform.Steam };
        var report = doctor.Diagnose(EditionSpec.Enhanced, edSettings);
        var eosConflict = report.Checks.FirstOrDefault(c => c.Id == "epic-eos-dll");
        Assert.NotNull(eosConflict);
        Assert.Equal(DoctorFixType.RepairEpicDllConflict, eosConflict!.FixType);

        var fixRes = await doctor.ApplyFixAsync(eosConflict, EditionSpec.Enhanced, edSettings);
        Assert.True(fixRes.Success, fixRes.Message);
        Assert.False(File.Exists(Path.Combine(gameDir, versionedEos)));
        Assert.True(File.Exists(Path.Combine(gameDir, versionedEos + ".epic")));
    }

    [Fact]
    public async Task EpicManifestService_SyncsEoshEgi_UpdatesAllFiveItemPaths_PrunesBps_CleansOrphans_And_RollsBack()
    {
        var gameDir = CreateMockGameDir(EditionSpec.Legacy);
        File.WriteAllText(Path.Combine(gameDir, "steam_api64.dll"), "STEAM_API");

        // Pre-existing corrupted bps in target gameDir from an older attempt
        var staleTargetBpsFile = Path.Combine(gameDir, ".egstore", "bps", "m", "$resumeData");
        Directory.CreateDirectory(Path.GetDirectoryName(staleTargetBpsFile)!);
        File.WriteAllText(staleTargetBpsFile, "STALE_TARGET_RESUME");

        // Temporary Epic folder containing Pending manifest + bps resume state
        const string primaryGuid = "C22863E7019B958C11A108E1211873E8";
        const string orphanGuid = "EBA5C0D52FB8671DA7A01F8425D9E85B";
        var tempEpicDir = Path.Combine(_tempRoot, "TempEpicEosh");
        Directory.CreateDirectory(Path.Combine(tempEpicDir, ".egstore", "Pending"));
        Directory.CreateDirectory(Path.Combine(tempEpicDir, ".egstore", "bps", "m"));
        File.WriteAllText(Path.Combine(tempEpicDir, ".egstore", "Pending", $"{primaryGuid}.manifest"), "EPIC_MANIFEST_DATA");
        File.WriteAllText(Path.Combine(tempEpicDir, ".egstore", "bps", "m", "$resumeData"), "ZERO_PERCENT_RESUME");

        var manifestsDir = Path.Combine(_tempRoot, "EpicSandbox", "Manifests");
        var pendingManifestsDir = Path.Combine(manifestsDir, "Pending");
        var eoshDir = Path.Combine(_tempRoot, "EpicSandbox", "InstalledItems");
        var launcherDatPath = Path.Combine(_tempRoot, "EpicSandbox", "LauncherInstalled.dat");
        Directory.CreateDirectory(pendingManifestsDir);
        Directory.CreateDirectory(eoshDir);

        var escapedTemp = tempEpicDir.Replace("\\", "\\\\");
        var primaryItemPath = Path.Combine(manifestsDir, $"{primaryGuid}.item");
        var orphanPendingItemPath = Path.Combine(pendingManifestsDir, $"{orphanGuid}.item");
        var primaryItemJson = $$"""
            {
              "FormatVersion": 0,
              "bIsIncompleteInstall": true,
              "AppName": "9d2d0eb64d5c44529cece33fe2a46482",
              "DisplayName": "Grand Theft Auto V",
              "InstallationGuid": "{{primaryGuid}}",
              "InstallLocation": "{{escapedTemp}}",
              "ManifestLocation": "{{escapedTemp}}\\.egstore",
              "CompleteManifestPath": "{{escapedTemp}}\\.egstore\\{{primaryGuid}}.manifest",
              "PendingManifestPath": "{{escapedTemp}}\\.egstore\\Pending\\{{primaryGuid}}.manifest",
              "StagingLocation": "{{escapedTemp}}\\.egstore/bps"
            }
            """;
        var orphanItemJson = $$"""
            {
              "FormatVersion": 0,
              "bIsIncompleteInstall": true,
              "AppName": "9d2d0eb64d5c44529cece33fe2a46482",
              "DisplayName": "Grand Theft Auto V",
              "InstallationGuid": "{{orphanGuid}}",
              "InstallLocation": "{{escapedTemp}}"
            }
            """;
        File.WriteAllText(primaryItemPath, primaryItemJson);
        File.WriteAllText(orphanPendingItemPath, orphanItemJson);

        var primaryEgiPath = Path.Combine(eoshDir, $"{primaryGuid}.egi");
        var orphanEgiPath = Path.Combine(eoshDir, $"{orphanGuid}.egi");
        var primaryEgiJson = $"{{\"v4\":{{\"installationId\":\"{primaryGuid}\",\"state\":\"Incomplete\",\"dir\":\"{escapedTemp}\",\"metaDir\":\"{escapedTemp}\\\\.egstore\",\"manifestPath\":\"{escapedTemp}\\\\.egstore\\\\{primaryGuid}.manifest\",\"pendingManifestPath\":\"{escapedTemp}\\\\.egstore\\\\Pending\\\\{primaryGuid}.manifest\",\"artifactId\":\"9d2d0eb64d5c44529cece33fe2a46482\"}}}}";
        var orphanEgiJson = $"{{\"v4\":{{\"installationId\":\"{orphanGuid}\",\"state\":\"Incomplete\",\"dir\":\"{escapedTemp}\",\"artifactId\":\"9d2d0eb64d5c44529cece33fe2a46482\"}}}}";
        File.WriteAllText(primaryEgiPath, primaryEgiJson);
        File.WriteAllText(orphanEgiPath, orphanEgiJson);

        var guard = new ProcessGuard(_ => false);
        var detector = new EditionDetector(_logger, appBaseDirectory: _tempRoot, epicManifestsDirectory: manifestsDir);
        var switcher = new PlatformSwitchService(detector, guard, _logger);
        var epicSvc = new EpicManifestService(guard, _logger, manifestsDir, launcherDatPath, eoshDir);

        // 1. Mid-operation failure restores .item, .egi, orphan files, steam_api64.dll, and pre-existing target bps
        var failRes = await epicSvc.RelocateAndAdoptAsync(
            EditionSpec.Legacy,
            gameDir,
            switcher,
            testFailBeforeLauncherInstalledWrite: () => throw new IOException("Simulated EOSH/LauncherInstalled failure"));
        Assert.False(failRes.Success);
        Assert.Equal(primaryItemJson, File.ReadAllText(primaryItemPath));
        Assert.Equal(primaryEgiJson, File.ReadAllText(primaryEgiPath));
        Assert.True(File.Exists(orphanPendingItemPath));
        Assert.True(File.Exists(orphanEgiPath));
        Assert.True(File.Exists(staleTargetBpsFile));
        Assert.True(File.Exists(Path.Combine(gameDir, "steam_api64.dll")));

        // 2. Successful adoption updates all 5 .item paths, all 4 .egi paths, prunes bps, and cleans orphan duplicates
        var okRes = await epicSvc.RelocateAndAdoptAsync(EditionSpec.Legacy, gameDir, switcher);
        Assert.True(okRes.Success, okRes.Message);

        Assert.True(File.Exists(Path.Combine(gameDir, ".egstore", "Pending", $"{primaryGuid}.manifest")));
        Assert.False(Directory.Exists(Path.Combine(gameDir, ".egstore", "bps")));
        Assert.False(File.Exists(orphanPendingItemPath));
        Assert.False(File.Exists(orphanEgiPath));

        using (var itemDoc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(primaryItemPath)))
        {
            var r = itemDoc.RootElement;
            Assert.Equal(gameDir, r.GetProperty("InstallLocation").GetString());
            Assert.Equal(Path.Combine(gameDir, ".egstore", $"{primaryGuid}.manifest"), r.GetProperty("CompleteManifestPath").GetString());
            Assert.Equal(Path.Combine(gameDir, ".egstore", "Pending", $"{primaryGuid}.manifest"), r.GetProperty("PendingManifestPath").GetString());
        }

        using (var egiDoc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(primaryEgiPath)))
        {
            var v4 = egiDoc.RootElement.GetProperty("v4");
            Assert.Equal(gameDir, v4.GetProperty("dir").GetString());
            Assert.Equal(Path.Combine(gameDir, ".egstore"), v4.GetProperty("metaDir").GetString());
            Assert.Equal(Path.Combine(gameDir, ".egstore", $"{primaryGuid}.manifest"), v4.GetProperty("manifestPath").GetString());
            Assert.Equal(Path.Combine(gameDir, ".egstore", "Pending", $"{primaryGuid}.manifest"), v4.GetProperty("pendingManifestPath").GetString());
        }

        var afterInspect = epicSvc.Inspect(EditionSpec.Legacy, gameDir);
        Assert.Equal(EpicManifestState.Aligned, afterInspect.State);
        Assert.False(afterInspect.HasOrphanDuplicates);
    }

    [Fact]
    public async Task RelocateAndAdoptFromFolderAsync_UsesExplicitSourceFolder_And_ProcessGuardChecksEoshHelper()
    {
        // ProcessGuard blocks on EpicOnlineServicesInstallHelper but ignores orphaned EpicWebHelper
        var eoshGuard = new ProcessGuard(name => name.Equals("EpicOnlineServicesInstallHelper", StringComparison.OrdinalIgnoreCase));
        Assert.True(eoshGuard.IsEpicGamesLauncherRunning());

        var webHelperOnlyGuard = new ProcessGuard(name => name.Equals("EpicWebHelper", StringComparison.OrdinalIgnoreCase));
        Assert.False(webHelperOnlyGuard.IsEpicGamesLauncherRunning());

        var gameDir = CreateMockGameDir(EditionSpec.Legacy);
        File.WriteAllText(Path.Combine(gameDir, "steam_api64.dll"), "STEAM_API");

        // Existing .item manifest points to an old folder without .egstore, while user selects a fresh temp folder
        var oldEmptyEpicDir = Path.Combine(_tempRoot, "OldDeletedEpicTemp");
        Directory.CreateDirectory(oldEmptyEpicDir);

        var freshSelectedTemp = Path.Combine(_tempRoot, "FreshUserSelectedTemp");
        Directory.CreateDirectory(Path.Combine(freshSelectedTemp, ".egstore"));
        File.WriteAllText(Path.Combine(freshSelectedTemp, ".egstore", "fresh.manifest"), "FRESH_MANIFEST");

        var manifestsDir = Path.Combine(_tempRoot, "Manifests_FromFolder");
        Directory.CreateDirectory(manifestsDir);
        File.WriteAllText(Path.Combine(manifestsDir, "existing.item"), $$"""
            {
              "AppName": "9d2d0eb64d5c44529cece33fe2a46482",
              "DisplayName": "Grand Theft Auto V",
              "InstallLocation": "{{oldEmptyEpicDir.Replace("\\", "\\\\")}}"
            }
            """);

        var detector = new EditionDetector(_logger, appBaseDirectory: _tempRoot, epicManifestsDirectory: manifestsDir);
        var switcher = new PlatformSwitchService(detector, webHelperOnlyGuard, _logger);
        var epicSvc = new EpicManifestService(webHelperOnlyGuard, _logger, manifestsDir);

        var adoptRes = await epicSvc.RelocateAndAdoptFromFolderAsync(EditionSpec.Legacy, gameDir, freshSelectedTemp, switcher);
        Assert.True(adoptRes.Success, adoptRes.Message);
        Assert.True(File.Exists(Path.Combine(gameDir, ".egstore", "fresh.manifest")));
        Assert.Equal(EpicManifestState.Aligned, epicSvc.Inspect(EditionSpec.Legacy, gameDir).State);
    }

    private string CreateMockGameDir(EditionSpec edition)
    {
        var dir = Path.Combine(_tempRoot, $"Game_{edition.ProfileFolderName}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, edition.MainExecutable), "MOCK_EXE");
        return dir;
    }
}
