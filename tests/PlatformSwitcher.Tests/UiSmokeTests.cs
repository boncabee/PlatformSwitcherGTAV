using System.Drawing;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Grand_Theft_Auto_V_Platform_Switcher;
using Grand_Theft_Auto_V_Platform_Switcher.Models;
using Grand_Theft_Auto_V_Platform_Switcher.Services;
using Grand_Theft_Auto_V_Platform_Switcher.UI;
using Grand_Theft_Auto_V_Platform_Switcher.UI.Controls;
using Grand_Theft_Auto_V_Platform_Switcher.UI.Views;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace PlatformSwitcher.Tests;

public sealed class UiSmokeTests : IDisposable
{
    private readonly string _tempRoot;

    [ModuleInitializer]
    internal static void InitializeWinForms()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
    }

    public UiSmokeTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"PSGTAV_UiSmoke_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
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
    public void ThemeManager_AllSemanticPairs_MeetWcagAA()
    {
        foreach (var palette in new[] { ThemeManager.Dark, ThemeManager.Light })
        {
            var label = palette.IsDark ? "Dark" : "Light";

            void AssertTextContrast(Color fg, Color bg, string pairName, double minRatio = 4.5)
            {
                var ratio = ThemeManager.GetContrastRatio(fg, bg);
                Assert.True(ratio >= minRatio, $"[{label}] '{pairName}' contrast {ratio:F2}:1 is below {minRatio:F1}:1.");
            }

            AssertTextContrast(palette.TextPrimary, palette.WindowBg, "TextPrimary/WindowBg");
            AssertTextContrast(palette.TextPrimary, palette.SurfaceBg, "TextPrimary/SurfaceBg");
            AssertTextContrast(palette.TextPrimary, palette.CardBg, "TextPrimary/CardBg");
            AssertTextContrast(palette.TextPrimary, palette.CardActiveBg, "TextPrimary/CardActiveBg");
            AssertTextContrast(palette.TextPrimary, palette.InputBg, "TextPrimary/InputBg");
            AssertTextContrast(palette.TextPrimary, palette.ButtonBg, "TextPrimary/ButtonBg");
            AssertTextContrast(palette.TextPrimary, palette.ButtonHoverBg, "TextPrimary/ButtonHoverBg");
            AssertTextContrast(palette.TextSecondary, palette.WindowBg, "TextSecondary/WindowBg");
            AssertTextContrast(palette.TextSecondary, palette.SurfaceBg, "TextSecondary/SurfaceBg");
            AssertTextContrast(palette.TextSecondary, palette.CardBg, "TextSecondary/CardBg");
            AssertTextContrast(palette.TextSecondary, palette.CardActiveBg, "TextSecondary/CardActiveBg");

            AssertTextContrast(Color.White, palette.Accent, "White/Accent");
            AssertTextContrast(Color.White, palette.AccentHover, "White/AccentHover");
            AssertTextContrast(Color.White, palette.Success, "White/Success");
            AssertTextContrast(Color.White, palette.SuccessHover, "White/SuccessHover");
            AssertTextContrast(Color.White, palette.Warning, "White/Warning");
            AssertTextContrast(Color.White, palette.Danger, "White/Danger");
            AssertTextContrast(Color.White, palette.DangerHover, "White/DangerHover");

            AssertTextContrast(palette.AccentText, palette.AccentSubtleBg, "AccentText/AccentSubtleBg");
            AssertTextContrast(palette.AccentText, palette.SurfaceBg, "AccentText/SurfaceBg");
            AssertTextContrast(palette.AccentText, palette.CardBg, "AccentText/CardBg");
            AssertTextContrast(palette.SuccessText, palette.SuccessSubtleBg, "SuccessText/SuccessSubtleBg");
            AssertTextContrast(palette.SuccessText, palette.SurfaceBg, "SuccessText/SurfaceBg");
            AssertTextContrast(palette.WarningText, palette.WarningSubtleBg, "WarningText/WarningSubtleBg");
            AssertTextContrast(palette.WarningText, palette.SurfaceBg, "WarningText/SurfaceBg");
            AssertTextContrast(palette.DangerText, palette.DangerSubtleBg, "DangerText/DangerSubtleBg");
            AssertTextContrast(palette.DangerText, palette.SurfaceBg, "DangerText/SurfaceBg");

            AssertTextContrast(palette.BorderStrong, palette.SurfaceBg, "BorderStrong/SurfaceBg", 3.0);
            AssertTextContrast(palette.BorderStrong, palette.CardBg, "BorderStrong/CardBg", 3.0);
            AssertTextContrast(palette.FocusRing, palette.SurfaceBg, "FocusRing/SurfaceBg", 3.0);
            AssertTextContrast(palette.FocusRing, palette.CardBg, "FocusRing/CardBg", 3.0);
        }
    }

    [Fact]
    public Task MainWindow_InlineNavigation_ThemesBoundsLongPaths_And_BusyGuard()
    {
        return RunOnStaThreadAsync(async () =>
        {
            var (window, legacyDir, enhancedDir) = CreateReadyTestWindow(useDarkTheme: true);
            try
            {
                Assert.True(legacyDir.Length > 110);
                Assert.True(enhancedDir.Length > 110);

                window.StartPosition = FormStartPosition.Manual;
                window.Location = new Point(-2000, -2000);
                window.Show();
                Application.DoEvents();

                await window.RefreshStateForTestAsync();
                Application.DoEvents();

                AssertSingleWindow(window);

                // Exercise long >220-char path in status bar to verify single-line ellipsis height
                var longStatus = $"Game folder updated to {enhancedDir}\\ExtraLongSubdirectory_ForStatusBarSingleLineEllipsisVerification_1234567890_ABCDEFGHIJKLMNOPQRSTUVWXYZ";
                Assert.True(longStatus.Length > 220);
                window.SetStatusForTest(longStatus);
                Application.DoEvents();

                // Verify outdated active badge text
                window.UpdateCardForTest(GamePlatform.RGL, "1.0.3258.0", hasProfile: true, isOutdated: true, activePlatform: GamePlatform.RGL);
                Assert.Equal("Game updated", window.GetCardForTest(GamePlatform.RGL).StatusPillForTest.Text);
                await window.RefreshStateForTestAsync();
                Application.DoEvents();

                // Verify SetBusy locks all guarded controls
                window.SetBusyForTest(true);
                foreach (var ctrl in window.GetBusyGuardedControlsForTest())
                {
                    Assert.False(ctrl.Enabled, $"Expected control '{ctrl.Text}' to be disabled when busy.");
                }
                window.SetBusyForTest(false);

                // Verify fixed-size compact shell contract
                Assert.Equal(FormBorderStyle.FixedSingle, window.FormBorderStyle);
                Assert.False(window.MaximizeBox);
                Assert.Equal(Size.Empty, window.MinimumSize);
                if (window.DeviceDpi == 96)
                {
                    Assert.Equal(new Size(730, 370), window.ClientSize);
                }

                // Test Dark & Light themes across all 3 inline views at the fixed compact size with STRICT zero-scroll at 100% DPI
                foreach (var isDark in new[] { true, false })
                {
                    if ((window.BackColor == ThemeManager.Dark.WindowBg) != isDark)
                    {
                        await window.ToggleThemeForTestAsync();
                        Application.DoEvents();
                    }

                    var themeName = isDark ? "dark" : "light";
                    var expectedBg = isDark ? ThemeManager.Dark.WindowBg : ThemeManager.Light.WindowBg;
                    Assert.Equal(expectedBg, window.BackColor);

                    // 1. Launch view
                    window.NavigateToLaunchForTest();
                    window.UpdateLayoutConstraints();
                    window.PerformLayout();
                    Application.DoEvents();
                    Assert.Equal(AppView.Launch, window.CurrentViewForTest);
                    AssertViewLayoutValid(window, $"Launch-{themeName}-Compact", requireZeroScroll: true);
                    AssertNoCrypticJargon(window, $"Launch-{themeName}");

                    // 2. Health view
                    await window.NavigateToHealthForTestAsync();
                    Application.DoEvents();
                    AssertSingleWindow(window);
                    Assert.Equal(AppView.Health, window.CurrentViewForTest);
                    Assert.True(window.DoctorViewForTest.ChecksListPanelForTest.RowCount >= 5);
                    Assert.False(window.DoctorViewForTest.IsBusyForTest);
                    AssertViewLayoutValid(window, $"Health-{themeName}-Compact", requireZeroScroll: true);
                    AssertNoCrypticJargon(window, $"Health-{themeName}");

                    // Navigate back via Launch tab
                    window.LaunchTabForTest.PerformClick();
                    Application.DoEvents();
                    AssertSingleWindow(window);
                    Assert.Equal(AppView.Launch, window.CurrentViewForTest);

                    // 3. Settings view (includes About section)
                    window.NavigateToSettingsForTest();
                    Application.DoEvents();
                    AssertSingleWindow(window);
                    Assert.Equal(AppView.Settings, window.CurrentViewForTest);
                    Assert.Equal(legacyDir, window.SettingsViewForTest.LegacyPathTextBoxForTest.Text);
                    Assert.Equal(enhancedDir, window.SettingsViewForTest.EnhancedPathTextBoxForTest.Text);
                    AssertViewLayoutValid(window, $"Settings-{themeName}-Compact", requireZeroScroll: true);
                    AssertNoCrypticJargon(window, $"Settings-{themeName}");

                    window.LaunchTabForTest.PerformClick();
                    Application.DoEvents();
                    AssertSingleWindow(window);
                    Assert.Equal(AppView.Launch, window.CurrentViewForTest);
                }

                window.Close();
            }
            finally
            {
                if (!window.IsDisposed)
                {
                    window.Dispose();
                }
            }
        });
    }

    [Fact]
    public Task MainWindow_AllUxStates_ZeroScroll_And_VisualCaptures()
    {
        return RunOnStaThreadAsync(async () =>
        {
            // 1. Ready state (Dark & Light) + Settings (Dark & Light)
            {
                var (window, _, enhancedDir) = CreateReadyTestWindow(useDarkTheme: true);
                try
                {
                    window.StartPosition = FormStartPosition.Manual;
                    window.Location = new Point(-2000, -2000);
                    window.Show();
                    Application.DoEvents();
                    await window.RefreshStateForTestAsync();
                    Application.DoEvents();

                    AssertViewLayoutValid(window, "State-Ready-Dark", requireZeroScroll: true);
                    CaptureFormIfRequested(window, "launch_ready_dark.png");

                    window.NavigateToSettingsForTest();
                    window.SetStatusForTest($"Game folder updated to {enhancedDir}\\VeryLongPathSegment_1234567890_ABCDEFGHIJKLMNOPQRSTUVWXYZ");
                    Application.DoEvents();
                    AssertViewLayoutValid(window, "State-Settings-Dark", requireZeroScroll: true);
                    CaptureFormIfRequested(window, "settings_dark.png");

                    await window.ToggleThemeForTestAsync();
                    Application.DoEvents();
                    AssertViewLayoutValid(window, "State-Settings-Light", requireZeroScroll: true);
                    CaptureFormIfRequested(window, "settings_light.png");

                    window.NavigateToLaunchForTest();
                    Application.DoEvents();
                    AssertViewLayoutValid(window, "State-Ready-Light", requireZeroScroll: true);
                    CaptureFormIfRequested(window, "launch_ready_light.png");

                    window.Close();
                }
                finally
                {
                    if (!window.IsDisposed) window.Dispose();
                }
            }

            // 2. Beginner state (no GameDirectory, Unknown platform)
            {
                var window = CreateCustomStateWindow(setup: (_, _) => { }, selectedEdition: GameEdition.Legacy, configuredPlatform: GamePlatform.Unknown, useDarkTheme: true);
                try
                {
                    window.StartPosition = FormStartPosition.Manual;
                    window.Location = new Point(-2000, -2000);
                    window.Show();
                    Application.DoEvents();
                    await window.RefreshStateForTestAsync();
                    Application.DoEvents();

                    Assert.False(window.GetCardForTest(GamePlatform.RGL).PrimaryButtonForTest.Enabled);
                    AssertViewLayoutValid(window, "State-Beginner-Dark", requireZeroScroll: true);
                    AssertNoCrypticJargon(window, "State-Beginner");
                    CaptureFormIfRequested(window, "launch_beginner_dark.png");

                    window.Close();
                }
                finally
                {
                    if (!window.IsDisposed) window.Dispose();
                }
            }

            // 3. Outdated state (active RGL + older Steam card)
            {
                var (window, _, _) = CreateReadyTestWindow(useDarkTheme: true);
                try
                {
                    window.StartPosition = FormStartPosition.Manual;
                    window.Location = new Point(-2000, -2000);
                    window.Show();
                    Application.DoEvents();
                    await window.RefreshStateForTestAsync();
                    Application.DoEvents();

                    window.UpdateCardForTest(GamePlatform.RGL, "1.0.3258.0", hasProfile: true, isOutdated: true, activePlatform: GamePlatform.RGL);
                    window.UpdateCardForTest(GamePlatform.Steam, "1.0.3258.0", hasProfile: true, isOutdated: true, activePlatform: GamePlatform.RGL);
                    Application.DoEvents();

                    Assert.Equal("Game updated", window.GetCardForTest(GamePlatform.RGL).StatusPillForTest.Text);
                    Assert.Equal("Outdated files", window.GetCardForTest(GamePlatform.Steam).StatusPillForTest.Text);
                    AssertViewLayoutValid(window, "State-Outdated-Dark", requireZeroScroll: true);
                    CaptureFormIfRequested(window, "launch_outdated_dark.png");

                    window.Close();
                }
                finally
                {
                    if (!window.IsDisposed) window.Dispose();
                }
            }

            // 4. Invalid Epic + Epic manifest elsewhere + Worst-Case Health (all checks simultaneously!)
            {
                var manifestsDir = Path.Combine(_tempRoot, $"EpicManifests_Worst_{Guid.NewGuid():N}");
                Directory.CreateDirectory(manifestsDir);
                var tempEpicDir = Path.Combine(_tempRoot, "TempEpicWorst");
                Directory.CreateDirectory(Path.Combine(tempEpicDir, ".egstore"));
                File.WriteAllText(Path.Combine(tempEpicDir, ".egstore", "item.manifest"), "MANIFEST");
                File.WriteAllText(Path.Combine(manifestsDir, "gtav.item"), $$"""
                    {
                      "AppName": "9d2d0eb64d5c44529cece33fe2a46482",
                      "DisplayName": "Grand Theft Auto V",
                      "InstallLocation": "{{tempEpicDir.Replace("\\", "\\\\")}}"
                    }
                    """);

                var window = CreateCustomStateWindow(
                    setup: (gameDir, appBase) =>
                    {
                        File.WriteAllText(Path.Combine(gameDir, "GTA5.exe"), "LEGACY_EXE");
                        // Polluted EGL backup (missing EOSSDK in both profile and gameDir)
                        var badEgl = Path.Combine(appBase, "Switcher", "Legacy", "EGL");
                        Directory.CreateDirectory(badEgl);
                        File.WriteAllText(Path.Combine(badEgl, "GTA5.exe"), "BAD_EGL");
                        // Steam DLL conflict
                        File.WriteAllText(Path.Combine(gameDir, "steam_api64.dll"), "STEAM");
                        File.WriteAllText(Path.Combine(gameDir, "steam_api64.dll.steam"), "STEAM_BACK");
                    },
                    selectedEdition: GameEdition.Legacy,
                    configuredPlatform: GamePlatform.Steam,
                    useDarkTheme: true,
                    manifestsDir: manifestsDir);
                try
                {
                    window.StartPosition = FormStartPosition.Manual;
                    window.Location = new Point(-2000, -2000);
                    window.Show();
                    Application.DoEvents();
                    await window.RefreshStateForTestAsync();
                    Application.DoEvents();

                    Assert.Equal("Needs repair", window.GetCardForTest(GamePlatform.EGL).StatusPillForTest.Text);
                    Assert.Equal("Fix Epic Setup", window.GetCardForTest(GamePlatform.EGL).PrimaryButtonForTest.Text);
                    AssertViewLayoutValid(window, "State-InvalidEpic-Dark", requireZeroScroll: true);
                    CaptureFormIfRequested(window, "launch_invalid_epic_dark.png");

                    // Worst-case Health view (8 checks + Fix buttons + non-admin Registry strip) at fixed compact size!
                    await window.NavigateToHealthForTestAsync();
                    Application.DoEvents();
                    Assert.True(window.DoctorViewForTest.ChecksListPanelForTest.RowCount >= 8);
                    AssertViewLayoutValid(window, "State-HealthWorst-Dark-Compact", requireZeroScroll: true);
                    CaptureFormIfRequested(window, "health_worst_case_dark.png");

                    await window.ToggleThemeForTestAsync();
                    Application.DoEvents();
                    AssertViewLayoutValid(window, "State-HealthWorst-Light-Compact", requireZeroScroll: true);
                    CaptureFormIfRequested(window, "health_worst_case_light.png");

                    window.Close();
                }
                finally
                {
                    if (!window.IsDisposed) window.Dispose();
                }
            }

            // 5. Guided EpicSetupDialog (zero-scroll + no jargon)
            {
                var guard = new ProcessGuard(_ => false);
                using var epicDialog = new EpicSetupDialog(EditionSpec.Legacy, guard, ThemeManager.Dark);
                epicDialog.StartPosition = FormStartPosition.Manual;
                epicDialog.Location = new Point(-2000, -2000);
                epicDialog.Show();
                Application.DoEvents();
                AssertViewLayoutValid(epicDialog, "EpicSetupDialog-Dark", requireZeroScroll: true);
                AssertNoCrypticJargon(epicDialog, "EpicSetupDialog-Dark");
                CaptureFormIfRequested(epicDialog, "epic_setup_dialog_dark.png");
                epicDialog.Close();
            }
        });
    }

    [Theory]
    [InlineData(1.5f, true, "DPI-150")]
    [InlineData(1.75f, true, "DPI-175")]
    [InlineData(1.35f, false, "FontOnly-135")]
    public Task MainWindow_SimulatedDpiAndFontScaling_AllViews_NoOverlapOrClipping(
        float scaleFactor,
        bool scaleWindowBounds,
        string scenarioLabel)
    {
        return RunOnStaThreadAsync(async () =>
        {
            var (window, _, _) = CreateReadyTestWindow(useDarkTheme: true);
            try
            {
                window.StartPosition = FormStartPosition.Manual;
                window.Location = new Point(-3000, -3000);
                window.Show();
                Application.DoEvents();

                await window.RefreshStateForTestAsync();
                Application.DoEvents();

                var baseFontSize = window.Font.Size;
                var longStatus = @"Game folder updated to C:\Games\Grand Theft Auto V\VeryLongDirectoryName_TestingSingleLineStatusBarEllipsis_1234567890_ABCDEFGHIJKLMNOPQRSTUVWXYZ_0987654321_ExtraLongSegmentForTruncationVerification_AdditionalPaddingForOver220Characters";
                Assert.True(longStatus.Length > 220);
                window.SetStatusForTest(longStatus);

                var (wasClamped, isProportional) = ApplySimulatedScale(window, scaleFactor, scaleWindowBounds);
                Application.DoEvents();

                Assert.True(window.Font.Size >= baseFontSize * scaleFactor - 0.1f,
                    $"[{scenarioLabel}] Expected distinct font scaling by {scaleFactor}x from {baseFontSize}pt, got {window.Font.Size}pt.");

                var enforceZeroScroll = scaleWindowBounds && !wasClamped && isProportional;

                // Launch
                window.NavigateToLaunchForTest();
                window.UpdateLayoutConstraints();
                window.PerformLayout();
                Application.DoEvents();
                AssertViewLayoutValid(window, $"{scenarioLabel}-Launch", enforceZeroScroll);

                // Health
                await window.NavigateToHealthForTestAsync();
                window.UpdateLayoutConstraints();
                window.PerformLayout();
                Application.DoEvents();
                AssertViewLayoutValid(window, $"{scenarioLabel}-Health", enforceZeroScroll);

                // Settings
                window.NavigateToSettingsForTest();
                window.UpdateLayoutConstraints();
                window.PerformLayout();
                Application.DoEvents();
                AssertViewLayoutValid(window, $"{scenarioLabel}-Settings", enforceZeroScroll);

                window.Close();
            }
            finally
            {
                if (!window.IsDisposed)
                {
                    window.Dispose();
                }
            }
        });
    }

    [Fact]
    public Task MainWindow_SettingsThemeSync_HealthFeedbackColor_And_PlainEnglishServices()
    {
        return RunOnStaThreadAsync(async () =>
        {
            var settingsDir = Path.Combine(_tempRoot, $"settings_sync_{Guid.NewGuid():N}");
            var logsDir = Path.Combine(_tempRoot, $"logs_sync_{Guid.NewGuid():N}");
            var logger = new AppLogger(logsDir);
            var settingsService = new SettingsService(logger, settingsDir);
            var initial = new AppSettings
            {
                SelectedEdition = GameEdition.Enhanced,
                UseDarkTheme = false,
                CheckUpdatesOnStartup = false,
                FirstRunDoctorShown = true
            };
            settingsService.Save(initial);

            var detector = new EditionDetector(logger, appBaseDirectory: _tempRoot, epicManifestsDirectory: Path.Combine(_tempRoot, "none"));
            var guard = new ProcessGuard();
            var switcher = new PlatformSwitchService(detector, guard, logger);
            var beSvc = new BattlEyeService(logger);
            var regSvc = new RegistryService(logger);
            var launcher = new GameLauncherService(detector, logger);
            var doctorSvc = new DoctorService(detector, switcher, beSvc, regSvc, guard);
            var updateSvc = new UpdateService(logger);

            using var window = new MainWindow(
                logger, settingsService, detector, guard, switcher, beSvc, regSvc, launcher, doctorSvc, updateSvc);
            window.StartPosition = FormStartPosition.Manual;
            window.Location = new Point(-2000, -2000);
            window.Show();
            Application.DoEvents();

            // 1. Open Settings in Light theme, instant-toggle to Dark via SegmentedControl
            window.NavigateToSettingsForTest();
            Application.DoEvents();
            Assert.Equal(1, window.SettingsViewForTest.ThemeToggleForTest.SelectedIndex);

            window.SettingsViewForTest.ThemeToggleForTest.LeftButton.PerformClick();
            Application.DoEvents();
            Assert.Equal(ThemeManager.Dark.WindowBg, window.BackColor);
            Assert.True(settingsService.Load().UseDarkTheme);

            // 2. Health warning feedback preserves semantic WarningText color across theme toggle
            await window.NavigateToHealthForTestAsync();
            Application.DoEvents();
            window.DoctorViewForTest.ShowInlineFeedbackForTest("Warning state test", isWarning: true);
            Assert.Equal(ThemeManager.Dark.WarningText, window.DoctorViewForTest.InlineFeedbackLabelForTest.ForeColor);

            await window.ToggleThemeForTestAsync();
            Application.DoEvents();
            Assert.Equal(ThemeManager.Light.WarningText, window.DoctorViewForTest.InlineFeedbackLabelForTest.ForeColor);

            // 3. Plain-English backend switch/launch messages contain no RGL/EGL/-nobattleye/URI launches/stale button names
            var gameDir = Path.Combine(_tempRoot, "PlainCopyGame");
            Directory.CreateDirectory(gameDir);
            File.WriteAllText(Path.Combine(gameDir, "GTA5_Enhanced.exe"), "MOCK");

            var unknownSwitch = await switcher.SwitchAsync(EditionSpec.Enhanced, gameDir, GamePlatform.Unknown);
            var emptySwitch = await switcher.SwitchAsync(EditionSpec.Enhanced, gameDir, GamePlatform.RGL);
            var unknownBackup = await switcher.BackupCurrentPlatformAsync(EditionSpec.Enhanced, gameDir, GamePlatform.Unknown);
            var okBackup = await switcher.BackupCurrentPlatformAsync(EditionSpec.Enhanced, gameDir, GamePlatform.RGL);
            var unknownPlan = launcher.BuildLaunchPlan(EditionSpec.Enhanced, gameDir, GamePlatform.Unknown, disableBattlEye: true);
            var steamPlan = launcher.BuildLaunchPlan(EditionSpec.Enhanced, gameDir, GamePlatform.Steam, disableBattlEye: true);
            var rglPlan = launcher.BuildLaunchPlan(EditionSpec.Enhanced, gameDir, GamePlatform.RGL, disableBattlEye: true);
            var eglPlan = launcher.BuildLaunchPlan(EditionSpec.Enhanced, gameDir, GamePlatform.EGL, disableBattlEye: true, epicAppIdOverride: "App123");

            foreach (var msg in new[]
            {
                unknownSwitch.Message,
                unknownBackup.Message,
                okBackup.Message,
                unknownPlan.Error ?? string.Empty,
                steamPlan.Notice ?? string.Empty,
                rglPlan.Notice ?? string.Empty,
                eglPlan.Notice ?? string.Empty
            })
            {
                Assert.DoesNotMatch(@"\bRGL\b", msg);
                Assert.DoesNotMatch(@"\bEGL\b", msg);
                Assert.DoesNotContain("-nobattleye", msg, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Backup Current Files", msg, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Save Backup", msg, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("URI launches", msg, StringComparison.OrdinalIgnoreCase);
            }

            Assert.Contains("Set up this launcher", emptySwitch.Message);

            window.Close();
        });
    }

    [Fact]
    public Task MainWindow_EpicAdoptionLifecycle_AwaitingVerify_And_PostVerifyVersionedSdk_ZeroScroll()
    {
        return RunOnStaThreadAsync(async () =>
        {
            var manifestsDir = Path.Combine(_tempRoot, $"EpicLifecycle_{Guid.NewGuid():N}");
            Directory.CreateDirectory(manifestsDir);
            string capturedGameDir = string.Empty;
            string capturedAppBase = string.Empty;

            var window = CreateCustomStateWindow(
                setup: (gameDir, appBase) =>
                {
                    capturedGameDir = gameDir;
                    capturedAppBase = appBase;

                    // Stage 1 post-adopt state: Steam backed up & parked, .egstore copied, Epic manifest & LauncherInstalled aligned, awaiting Epic verify
                    File.WriteAllText(Path.Combine(gameDir, "GTA5_Enhanced.exe"), "ENHANCED_EXE");
                    File.WriteAllText(Path.Combine(gameDir, "PlayGTAV.exe"), "PLAY_EXE");
                    File.WriteAllText(Path.Combine(gameDir, "steam_api64.dll.steam"), "PARKED_STEAM");
                    Directory.CreateDirectory(Path.Combine(gameDir, ".egstore"));
                    File.WriteAllText(Path.Combine(gameDir, ".egstore", "item.manifest"), "MANIFEST");

                    var steamBackup = Path.Combine(appBase, "Switcher", "Enhanced", "Steam");
                    Directory.CreateDirectory(steamBackup);
                    File.WriteAllText(Path.Combine(steamBackup, "GTA5_Enhanced.exe"), "STEAM_BACKUP_EXE");

                    var escapedGame = gameDir.Replace("\\", "\\\\");
                    var forwardGame = gameDir.Replace("\\", "/");
                    File.WriteAllText(Path.Combine(manifestsDir, "enhanced.item"), $$"""
                        {
                          "bIsIncompleteInstall": true,
                          "AppName": "8769e24080ea413b8ebca3f1b8c50951",
                          "DisplayName": "Grand Theft Auto V Enhanced",
                          "InstallLocation": "{{escapedGame}}",
                          "ManifestLocation": "{{forwardGame}}/.egstore"
                        }
                        """);

                    var runRoot = Path.GetDirectoryName(appBase)!;
                    File.WriteAllText(Path.Combine(runRoot, "LauncherInstalled.dat"), $$"""
                        {
                          "InstallationList": [
                            {
                              "InstallLocation": "{{escapedGame}}",
                              "AppName": "8769e24080ea413b8ebca3f1b8c50951"
                            }
                          ]
                        }
                        """);
                },
                selectedEdition: GameEdition.Enhanced,
                configuredPlatform: GamePlatform.Steam,
                useDarkTheme: true,
                manifestsDir: manifestsDir);

            try
            {
                window.StartPosition = FormStartPosition.Manual;
                window.Location = new Point(-2000, -2000);
                window.Show();
                Application.DoEvents();
                await window.RefreshStateForTestAsync();
                Application.DoEvents();

                // Stage 1: Epic card shows "Verify in Epic" + "Verify Guide", and RGL was NOT falsely polluted from parked Steam files
                var eglCard = window.GetCardForTest(GamePlatform.EGL);
                var rglCard = window.GetCardForTest(GamePlatform.RGL);
                Assert.Equal("Verify in Epic", eglCard.StatusPillForTest.Text);
                Assert.Equal("Verify Guide", eglCard.PrimaryButtonForTest.Text);
                Assert.Equal("Not set up", rglCard.StatusPillForTest.Text);
                Assert.False(Directory.Exists(Path.Combine(capturedAppBase, "Switcher", "Enhanced", "RGL")));

                AssertViewLayoutValid(window, "EpicLifecycle-AwaitingVerify-Dark", requireZeroScroll: true);
                AssertNoCrypticJargon(window, "EpicLifecycle-AwaitingVerify-Dark");

                // Stage 2: Epic finishes verifying and drops version-suffixed EOSSDK-Win64-Shipping-1.17.1.3.dll
                const string versionedEos = "EOSSDK-Win64-Shipping-1.17.1.3.dll";
                File.WriteAllText(Path.Combine(capturedGameDir, versionedEos), "ENHANCED_EPIC_SDK");

                await window.RefreshStateForTestAsync();
                Application.DoEvents();

                // Option A: Epic card highlights as active with "Epic ready" and 1-click "Save Epic Files"
                Assert.Equal("Epic ready", eglCard.StatusPillForTest.Text);
                Assert.Equal("Save Epic Files", eglCard.PrimaryButtonForTest.Text);
                AssertViewLayoutValid(window, "EpicLifecycle-PostVerifyReady-Dark", requireZeroScroll: true);
                AssertNoCrypticJargon(window, "EpicLifecycle-PostVerifyReady-Dark");

                // Clicking "Save Epic Files" saves versioned EOS SDK into EGL backup and transitions card to "Active" / "Play Now"
                eglCard.PrimaryButtonForTest.PerformClick();
                for (var i = 0; i < 40 && eglCard.PrimaryButtonForTest.Text != "Play Now"; i++)
                {
                    await Task.Delay(25);
                    Application.DoEvents();
                }

                Assert.True(File.Exists(Path.Combine(capturedAppBase, "Switcher", "Enhanced", "EGL", versionedEos)));
                Assert.Equal("Active", eglCard.StatusPillForTest.Text);
                Assert.Equal("Play Now", eglCard.PrimaryButtonForTest.Text);

                window.Close();
            }
            finally
            {
                if (!window.IsDisposed) window.Dispose();
            }
        });
    }

    [Fact]
    public Task MainWindow_SwitchAndPlaySeparated_SwitchDoesNotLaunch_And_TransitionsActiveCard()
    {
        return RunOnStaThreadAsync(async () =>
        {
            var (window, _, enhancedDir) = CreateReadyTestWindow(useDarkTheme: true);
            File.WriteAllText(Path.Combine(enhancedDir, "steam_api64.dll.steam"), "STEAM_PARKED");

            try
            {
                window.StartPosition = FormStartPosition.Manual;
                window.Location = new Point(-2000, -2000);
                window.Show();
                Application.DoEvents();
                await window.RefreshStateForTestAsync();
                Application.DoEvents();

                var rglCard = window.GetCardForTest(GamePlatform.RGL);
                var steamCard = window.GetCardForTest(GamePlatform.Steam);
                var eglCard = window.GetCardForTest(GamePlatform.EGL);

                // 1. Initial state: RGL active ("Play Now"), Steam ready ("Switch", NOT "Switch & Play"), EGL not set up
                Assert.Equal("Active", rglCard.StatusPillForTest.Text);
                Assert.Equal("Play Now", rglCard.PrimaryButtonForTest.Text);
                Assert.Equal(ActionButtonVariant.PrimarySuccess, rglCard.PrimaryButtonForTest.Variant);
                Assert.True(rglCard.CardPanelForTest.IsHighlighted);

                Assert.Equal("Ready", steamCard.StatusPillForTest.Text);
                Assert.Equal("Switch", steamCard.PrimaryButtonForTest.Text);
                Assert.NotEqual("Switch & Play", steamCard.PrimaryButtonForTest.Text);
                Assert.Equal(ActionButtonVariant.Secondary, steamCard.PrimaryButtonForTest.Variant);
                Assert.False(steamCard.CardPanelForTest.IsHighlighted);

                Assert.Equal("Not set up", eglCard.StatusPillForTest.Text);
                Assert.Equal("Set Up Epic...", eglCard.PrimaryButtonForTest.Text);
                Assert.NotEqual("Play Now", eglCard.PrimaryButtonForTest.Text);
                Assert.NotEqual("Switch", eglCard.PrimaryButtonForTest.Text);
                Assert.False(eglCard.CardPanelForTest.IsHighlighted);

                // 2. Overflow menu check: no "Switch only (don't launch)", while shortcut/refresh/open-folder items stay intact
                foreach (var card in new[] { rglCard, steamCard, eglCard })
                {
                    var menuTexts = card.OverflowMenuForTest.Items
                        .OfType<ToolStripMenuItem>()
                        .Select(item => item.Text ?? string.Empty)
                        .ToArray();

                    Assert.DoesNotContain(menuTexts, text => text.Contains("Switch only", StringComparison.OrdinalIgnoreCase));
                    Assert.Equal(new[] { "Create Desktop Shortcut", "Refresh saved files", "Open game folder" }, menuTexts);
                    Assert.Equal(4, card.OverflowMenuForTest.Items.Count);
                }

                Assert.True(rglCard.ShortcutMenuItemForTest.Enabled);
                Assert.True(rglCard.RefreshFilesMenuItemForTest.Enabled);
                Assert.True(rglCard.OpenFolderMenuItemForTest.Enabled);

                Assert.True(steamCard.ShortcutMenuItemForTest.Enabled);
                Assert.False(steamCard.RefreshFilesMenuItemForTest.Enabled);
                Assert.True(steamCard.OpenFolderMenuItemForTest.Enabled);

                AssertViewLayoutValid(window, "SwitchSeparated-Initial-Dark-Default", requireZeroScroll: true);
                AssertNoCrypticJargon(window, "SwitchSeparated-Initial-Dark-Default");

                // 3. Click Steam card's "Switch" button -> switches files and transitions active card WITHOUT launching game
                steamCard.PrimaryButtonForTest.PerformClick();
                for (var i = 0;
                     i < 80 && (steamCard.PrimaryButtonForTest.Text != "Play Now" ||
                                rglCard.PrimaryButtonForTest.Text != "Switch" ||
                                !steamCard.PrimaryButtonForTest.Enabled);
                     i++)
                {
                    await Task.Delay(25);
                    Application.DoEvents();
                }
                await Task.Delay(25);
                Application.DoEvents();

                Assert.Equal("Active", steamCard.StatusPillForTest.Text);
                Assert.Equal("Play Now", steamCard.PrimaryButtonForTest.Text);
                Assert.Equal(ActionButtonVariant.PrimarySuccess, steamCard.PrimaryButtonForTest.Variant);
                Assert.True(steamCard.CardPanelForTest.IsHighlighted);
                Assert.True(steamCard.RefreshFilesMenuItemForTest.Enabled);

                Assert.Equal("Ready", rglCard.StatusPillForTest.Text);
                Assert.Equal("Switch", rglCard.PrimaryButtonForTest.Text);
                Assert.NotEqual("Switch & Play", rglCard.PrimaryButtonForTest.Text);
                Assert.Equal(ActionButtonVariant.Secondary, rglCard.PrimaryButtonForTest.Variant);
                Assert.False(rglCard.CardPanelForTest.IsHighlighted);
                Assert.False(rglCard.RefreshFilesMenuItemForTest.Enabled);

                Assert.Equal("Not set up", eglCard.StatusPillForTest.Text);
                Assert.Equal("Set Up Epic...", eglCard.PrimaryButtonForTest.Text);
                Assert.False(eglCard.CardPanelForTest.IsHighlighted);

                Assert.Contains("switched", window.StatusLabelForTest.Text, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("Steam", window.StatusLabelForTest.Text, StringComparison.Ordinal);
                Assert.False(
                    window.StatusLabelForTest.Text.StartsWith("Launched ", StringComparison.OrdinalIgnoreCase),
                    $"Expected clicking 'Switch' not to launch game, but status was '{window.StatusLabelForTest.Text}'.");

                Assert.True(File.Exists(Path.Combine(enhancedDir, "steam_api64.dll")));
                Assert.False(File.Exists(Path.Combine(enhancedDir, "steam_api64.dll.steam")));
                Assert.Equal("STEAM_PROFILE", File.ReadAllText(Path.Combine(enhancedDir, "GTA5_Enhanced.exe")));

                // 4. Layout & plain-English checks at fixed compact size (730x370) + full-window screenshot capture
                AssertViewLayoutValid(window, "SwitchSeparated-Switched-Dark-Compact", requireZeroScroll: true);
                AssertNoCrypticJargon(window, "SwitchSeparated-Switched-Dark-Compact");
                CaptureFormIfRequested(window, "launch_switched_steam_dark.png");

                window.Close();
            }
            finally
            {
                if (!window.IsDisposed) window.Dispose();
            }
        });
    }

    private (MainWindow Window, string LegacyDir, string EnhancedDir) CreateReadyTestWindow(bool useDarkTheme)
    {
        var settingsDir = Path.Combine(_tempRoot, $"settings_{Guid.NewGuid():N}");
        var logsDir = Path.Combine(_tempRoot, $"logs_{Guid.NewGuid():N}");
        var baseLong = Path.Combine(
            _tempRoot,
            "VeryLongParentDirectoryName_ForTestingHighDpiWrapping",
            "Subfolder_GrandTheftAutoV_Installation_Directory_1234567890_ExtraSegment");
        var legacyDir = Path.Combine(baseLong, "LegacyEdition");
        var enhancedDir = Path.Combine(baseLong, "EnhancedEdition");
        Directory.CreateDirectory(legacyDir);
        Directory.CreateDirectory(enhancedDir);

        File.WriteAllText(Path.Combine(legacyDir, "GTA5.exe"), "MOCK_LEGACY");
        File.WriteAllText(Path.Combine(legacyDir, "steam_api64.dll"), "ORIG");
        File.WriteAllText(Path.Combine(legacyDir, "steam_api64.dll.steam"), "BACKUP");

        File.WriteAllText(Path.Combine(enhancedDir, "GTA5_Enhanced.exe"), "MOCK_ENHANCED");
        File.WriteAllText(Path.Combine(enhancedDir, "title.rgl"), "RGL");

        var rglProfile = Path.Combine(enhancedDir, "Switcher", "Enhanced", "RGL");
        Directory.CreateDirectory(rglProfile);
        File.WriteAllText(Path.Combine(rglProfile, "GTA5_Enhanced.exe"), "RGL_PROFILE");

        var steamProfile = Path.Combine(enhancedDir, "Switcher", "Enhanced", "Steam");
        Directory.CreateDirectory(steamProfile);
        File.WriteAllText(Path.Combine(steamProfile, "GTA5_Enhanced.exe"), "STEAM_PROFILE");

        var logger = new AppLogger(logsDir);
        var settingsService = new SettingsService(logger, settingsDir);
        var settings = new AppSettings
        {
            SelectedEdition = GameEdition.Enhanced,
            UseDarkTheme = useDarkTheme,
            CheckUpdatesOnStartup = false,
            FirstRunDoctorShown = true
        };
        settings.Legacy.GameDirectory = legacyDir;
        settings.Legacy.CurrentPlatform = GamePlatform.Steam;
        settings.Legacy.FirstRunDoctorShown = true;
        settings.Enhanced.GameDirectory = enhancedDir;
        settings.Enhanced.CurrentPlatform = GamePlatform.RGL;
        settings.Enhanced.FirstRunDoctorShown = true;
        settingsService.Save(settings);

        var detector = new EditionDetector(logger, appBaseDirectory: _tempRoot, epicManifestsDirectory: Path.Combine(_tempRoot, "none"));
        var guard = new ProcessGuard();
        var switcher = new PlatformSwitchService(detector, guard, logger);
        var beSvc = new BattlEyeService(logger);
        var regSvc = new RegistryService(logger);
        var launcher = new GameLauncherService(detector, logger);
        var doctorSvc = new DoctorService(detector, switcher, beSvc, regSvc, guard);
        var updateSvc = new UpdateService(logger);

        var window = new MainWindow(
            logger,
            settingsService,
            detector,
            guard,
            switcher,
            beSvc,
            regSvc,
            launcher,
            doctorSvc,
            updateSvc);

        return (window, legacyDir, enhancedDir);
    }

    private MainWindow CreateCustomStateWindow(
        Action<string, string> setup,
        GameEdition selectedEdition,
        GamePlatform configuredPlatform,
        bool useDarkTheme,
        string? manifestsDir = null)
    {
        var runRoot = Path.Combine(_tempRoot, $"Custom_{Guid.NewGuid():N}");
        var settingsDir = Path.Combine(runRoot, "settings");
        var logsDir = Path.Combine(runRoot, "logs");
        var appBase = Path.Combine(runRoot, "app");
        var gameDir = Path.Combine(runRoot, "Game");
        Directory.CreateDirectory(appBase);

        var hasGameDir = configuredPlatform != GamePlatform.Unknown;
        if (hasGameDir)
        {
            Directory.CreateDirectory(gameDir);
            setup(gameDir, appBase);
        }

        var logger = new AppLogger(logsDir);
        var settingsService = new SettingsService(logger, settingsDir);
        var settings = new AppSettings
        {
            SelectedEdition = selectedEdition,
            UseDarkTheme = useDarkTheme,
            CheckUpdatesOnStartup = false,
            FirstRunDoctorShown = true
        };
        if (hasGameDir)
        {
            var ed = settings.GetEditionSettings(selectedEdition);
            ed.GameDirectory = gameDir;
            ed.CurrentPlatform = configuredPlatform;
            ed.FirstRunDoctorShown = true;
        }
        settingsService.Save(settings);

        var epicDir = manifestsDir ?? Path.Combine(runRoot, "none");
        var detector = new EditionDetector(
            logger,
            appBaseDirectory: appBase,
            epicManifestsDirectory: epicDir,
            disableSystemAutoDetect: true);
        var guard = new ProcessGuard(_ => false);
        var switcher = new PlatformSwitchService(detector, guard, logger);
        var beSvc = new BattlEyeService(logger);
        var regSvc = new RegistryService(logger);
        var launcher = new GameLauncherService(detector, logger);
        var epicSvc = new EpicManifestService(guard, logger, epicDir, Path.Combine(runRoot, "LauncherInstalled.dat"));
        var doctorSvc = new DoctorService(detector, switcher, beSvc, regSvc, guard, epicSvc);
        var updateSvc = new UpdateService(logger);

        return new MainWindow(
            logger,
            settingsService,
            detector,
            guard,
            switcher,
            beSvc,
            regSvc,
            launcher,
            doctorSvc,
            updateSvc,
            epicSvc);
    }

    private static (bool WasClamped, bool IsProportional) ApplySimulatedScale(Form form, float scale, bool scaleWindowBounds)
    {
        var currentDpiScale = Math.Max(1f, form.DeviceDpi / 96f);
        var effectiveBoundScale = scaleWindowBounds && currentDpiScale > 1.05f
            ? Math.Max(1.1f, scale / currentDpiScale * 1.15f)
            : scale;

        var wasClamped = false;
        var isProportional = scaleWindowBounds && effectiveBoundScale >= scale - 0.05f;
        form.SuspendLayout();
        ScaleFontsRecursively(form, scale);
        if (scaleWindowBounds)
        {
            var workArea = Screen.FromControl(form).WorkingArea;
            var chromeW = Math.Max(0, form.Width - form.ClientSize.Width);
            var chromeH = Math.Max(0, form.Height - form.ClientSize.Height);
            var maxClientW = Math.Max(320, workArea.Width - chromeW);
            var maxClientH = Math.Max(240, workArea.Height - chromeH);
            var desiredClientW = (int)Math.Ceiling(form.ClientSize.Width * effectiveBoundScale);
            var desiredClientH = (int)Math.Ceiling(form.ClientSize.Height * effectiveBoundScale);
            wasClamped = desiredClientW > maxClientW || desiredClientH > maxClientH;
            var targetClientW = Math.Min(maxClientW, desiredClientW);
            var targetClientH = Math.Min(maxClientH, desiredClientH);
            if (form.MinimumSize != Size.Empty)
            {
                form.MinimumSize = new Size(
                    Math.Min(targetClientW + chromeW, (int)Math.Ceiling(form.MinimumSize.Width * effectiveBoundScale)),
                    Math.Min(targetClientH + chromeH, (int)Math.Ceiling(form.MinimumSize.Height * effectiveBoundScale)));
            }
            form.ClientSize = new Size(targetClientW, targetClientH);
        }
        form.ResumeLayout(true);

        if (form is MainWindow mw)
        {
            mw.UpdateLayoutConstraints();
        }
        form.PerformLayout();
        return (wasClamped, isProportional);
    }

    private static void ScaleFontsRecursively(Control root, float scale)
    {
        var snapshot = new List<(Control Control, Font Font)>();
        void Collect(Control c)
        {
            snapshot.Add((c, c.Font));
            foreach (Control child in c.Controls)
            {
                Collect(child);
            }
        }

        Collect(root);
        foreach (var (control, originalFont) in snapshot)
        {
            control.Font = new Font(originalFont.FontFamily, originalFont.Size * scale, originalFont.Style, originalFont.Unit);
        }
    }

    private static void AssertSingleWindow(Form expectedWindow)
    {
        var openCount = 0;
        foreach (Form f in Application.OpenForms)
        {
            if (f == expectedWindow) openCount++;
        }
        Assert.True(openCount == 1);
    }

    private static void AssertViewLayoutValid(Form hostForm, string contextLabel, bool requireZeroScroll)
    {
        AssertSingleWindow(hostForm);

        if (hostForm is MainWindow mw)
        {
            var statusLbl = mw.StatusLabelForTest;
            Assert.True(statusLbl.Height <= statusLbl.Font.Height + 24,
                $"[{contextLabel}] Status bar wrapped/bloated: Height={statusLbl.Height}, Font.Height={statusLbl.Font.Height}");
        }

        var leafRects = new List<(Control Control, Rectangle Rect)>();
        ValidateContainerRecursively(hostForm, hostForm, contextLabel, requireZeroScroll, leafRects);

        for (var i = 0; i < leafRects.Count; i++)
        {
            for (var j = i + 1; j < leafRects.Count; j++)
            {
                var (c1, r1) = leafRects[i];
                var (c2, r2) = leafRects[j];
                Assert.False(r1.IntersectsWith(r2),
                    $"[{contextLabel}] Visible leaf controls overlap in form coordinates: " +
                    $"'{c1.GetType().Name}({c1.Text})' {r1} vs '{c2.GetType().Name}({c2.Text})' {r2}");
            }
        }
    }

    private static void ValidateContainerRecursively(
        Control parent,
        Form hostForm,
        string contextLabel,
        bool requireZeroScroll,
        List<(Control Control, Rectangle Rect)> leafRects)
    {
        var visibleChildren = parent.Controls.Cast<Control>().Where(c => c.Visible).ToList();
        var isAutoScrollContainer = parent is ScrollableControl sc && sc.AutoScroll;

        if (parent is ScrollableControl scrollContainer && scrollContainer.AutoScroll)
        {
            Assert.False(scrollContainer.HorizontalScroll.Visible,
                $"[{contextLabel}] Unexpected horizontal scrollbar on '{scrollContainer.GetType().Name}' (Client={scrollContainer.ClientSize}, Display={scrollContainer.DisplayRectangle}).");
            Assert.True(scrollContainer.DisplayRectangle.Width <= scrollContainer.ClientSize.Width,
                $"[{contextLabel}] Horizontal overflow on '{scrollContainer.GetType().Name}': DisplayW={scrollContainer.DisplayRectangle.Width} > ClientW={scrollContainer.ClientSize.Width}");

            if (requireZeroScroll)
            {
                Assert.False(scrollContainer.VerticalScroll.Visible,
                    $"[{contextLabel}] Unexpected vertical scrollbar on '{scrollContainer.GetType().Name}' (Client={scrollContainer.ClientSize}, Display={scrollContainer.DisplayRectangle}).");
                Assert.True(scrollContainer.DisplayRectangle.Height <= scrollContainer.ClientSize.Height,
                    $"[{contextLabel}] Vertical overflow on '{scrollContainer.GetType().Name}': DisplayH={scrollContainer.DisplayRectangle.Height} > ClientH={scrollContainer.ClientSize.Height}");
            }
        }

        for (var i = 0; i < visibleChildren.Count; i++)
        {
            var child = visibleChildren[i];
            Assert.True(child.Width > 0 && child.Height > 0,
                $"[{contextLabel}] Control '{child.GetType().Name}({child.Text})' inside '{parent.GetType().Name}' has non-positive size: {child.Size}");

            Assert.True(child.Left >= 0,
                $"[{contextLabel}] Control '{child.GetType().Name}({child.Text})' clips left edge: Left={child.Left} inside {parent.GetType().Name}");
            Assert.True(child.Right <= parent.ClientSize.Width,
                $"[{contextLabel}] Control '{child.GetType().Name}({child.Text})' overflows right edge: Right={child.Right} > ClientWidth={parent.ClientSize.Width} inside {parent.GetType().Name}");

            if (requireZeroScroll || !isAutoScrollContainer)
            {
                Assert.True(child.Top >= 0,
                    $"[{contextLabel}] Control '{child.GetType().Name}({child.Text})' clips top edge: Top={child.Top} inside {parent.GetType().Name}");
                Assert.True(child.Bottom <= parent.ClientSize.Height,
                    $"[{contextLabel}] Control '{child.GetType().Name}({child.Text})' overflows bottom edge: Bottom={child.Bottom} > ClientHeight={parent.ClientSize.Height} inside {parent.GetType().Name}");
            }

            for (var j = i + 1; j < visibleChildren.Count; j++)
            {
                var other = visibleChildren[j];
                Assert.False(child.Bounds.IntersectsWith(other.Bounds),
                    $"[{contextLabel}] Sibling overlap inside {parent.GetType().Name}: " +
                    $"'{child.GetType().Name}({child.Text})' {child.Bounds} vs '{other.GetType().Name}({other.Text})' {other.Bounds}");
            }

            var hasVisibleChildren = child.Controls.Cast<Control>().Any(c => c.Visible);
            if (hasVisibleChildren)
            {
                ValidateContainerRecursively(child, hostForm, contextLabel, requireZeroScroll, leafRects);
            }
            else if (IsLeafUiControl(child))
            {
                AssertTextFitsControl(child, contextLabel);

                var formRect = GetVisibleFormClientRect(child, hostForm);
                if (formRect.Width > 0 && formRect.Height > 0)
                {
                    leafRects.Add((child, formRect));
                }
            }
        }
    }

    private static bool IsLeafUiControl(Control c) =>
        c is Button or Label or LinkLabel or CheckBox or TextBox or PictureBox;

    private static Rectangle GetVisibleFormClientRect(Control ctrl, Form hostForm)
    {
        var rect = hostForm.RectangleToClient(ctrl.RectangleToScreen(new Rectangle(Point.Empty, ctrl.Size)));
        var ancestor = ctrl.Parent;
        while (ancestor is not null && ancestor != hostForm)
        {
            if (ancestor is ScrollableControl sc && sc.AutoScroll)
            {
                var clip = hostForm.RectangleToClient(sc.RectangleToScreen(sc.ClientRectangle));
                rect = Rectangle.Intersect(rect, clip);
                if (rect.IsEmpty)
                {
                    return Rectangle.Empty;
                }
            }
            ancestor = ancestor.Parent;
        }
        return rect;
    }

    private static void AssertTextFitsControl(Control ctrl, string contextLabel)
    {
        if (string.IsNullOrWhiteSpace(ctrl.Text) || ctrl is TextBox)
        {
            return;
        }

        if (ctrl is Label lbl)
        {
            if (lbl.AutoEllipsis)
            {
                return;
            }

            var constraintW = lbl.MaximumSize.Width > 0 ? lbl.MaximumSize.Width : lbl.Width;
            var pref = lbl.GetPreferredSize(new Size(Math.Max(1, constraintW), 0));
            Assert.True(lbl.Width >= pref.Width && lbl.Height >= pref.Height,
                $"[{contextLabel}] Label text clipped: '{lbl.Text}' Size={lbl.Size}, Preferred={pref}");
            return;
        }

        if (ctrl is CheckBox chk)
        {
            var constraintW = chk.MaximumSize.Width > 0 ? chk.MaximumSize.Width : chk.Width;
            var pref = chk.GetPreferredSize(new Size(Math.Max(1, constraintW), 0));
            Assert.True(chk.Width >= pref.Width && chk.Height >= pref.Height,
                $"[{contextLabel}] CheckBox text clipped: '{chk.Text}' Size={chk.Size}, Preferred={pref}");
            return;
        }

        if (ctrl is LinkLabel link)
        {
            var pref = link.GetPreferredSize(Size.Empty);
            Assert.True(link.Width >= pref.Width && link.Height >= pref.Height,
                $"[{contextLabel}] LinkLabel text clipped: '{link.Text}' Size={link.Size}, Preferred={pref}");
            return;
        }

        if (ctrl is Button btn)
        {
            var singleText = TextRenderer.MeasureText(btn.Text, btn.Font, Size.Empty, TextFormatFlags.SingleLine);
            if (btn.Width >= singleText.Width + btn.Padding.Horizontal + 4 &&
                btn.Height >= singleText.Height + btn.Padding.Vertical + 4)
            {
                return;
            }

            var innerW = Math.Max(1, btn.Width - btn.Padding.Horizontal - 6);
            var measured = TextRenderer.MeasureText(
                btn.Text,
                btn.Font,
                new Size(innerW, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
            var longestWord = btn.Text
                .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(w => TextRenderer.MeasureText(w, btn.Font, Size.Empty, TextFormatFlags.SingleLine).Width)
                .DefaultIfEmpty(0)
                .Max();

            Assert.True(longestWord <= innerW && measured.Height + btn.Padding.Vertical <= btn.Height + 2,
                $"[{contextLabel}] Button text clipped: '{btn.Text}' Size={btn.Size}, SingleText={singleText}, WrappedMeasured={measured}, LongestWord={longestWord}, InnerW={innerW}");
        }
    }

    private static void AssertNoCrypticJargon(Control root, string contextLabel)
    {
        foreach (Control child in root.Controls)
        {
            if (!child.Visible)
            {
                continue;
            }

            if (child is not TextBox && !string.IsNullOrWhiteSpace(child.Text))
            {
                var text = child.Text;
                Assert.False(Regex.IsMatch(text, @"\bRGL\b", RegexOptions.IgnoreCase),
                    $"[{contextLabel}] Found cryptic 'RGL' jargon in UI text: '{text}'");
                Assert.False(Regex.IsMatch(text, @"\bEGL\b", RegexOptions.IgnoreCase),
                    $"[{contextLabel}] Found cryptic 'EGL' jargon in UI text: '{text}'");
                Assert.DoesNotContain("-nobattleye", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(".NET 8 WinForms", text, StringComparison.OrdinalIgnoreCase);
            }

            if (child.Controls.Count > 0)
            {
                AssertNoCrypticJargon(child, contextLabel);
            }
        }
    }

    private static void CaptureFormIfRequested(Form form, string fileName)
    {
        var captureDir = Environment.GetEnvironmentVariable("PSGTAV_UI_CAPTURE_DIR");
        if (string.IsNullOrWhiteSpace(captureDir))
        {
            return;
        }

        Directory.CreateDirectory(captureDir);
        form.PerformLayout();
        form.Update();
        form.Refresh();
        Application.DoEvents();

        using var bmp = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
        bmp.Save(Path.Combine(captureDir, fileName));
    }

    private static Task RunOnStaThreadAsync(Func<Task> action)
    {
        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            using var syncCtx = new WindowsFormsSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(syncCtx);
            void OnThreadException(object? sender, ThreadExceptionEventArgs e)
            {
                tcs.TrySetException(e.Exception);
                Application.ExitThread();
            }

            Application.ThreadException += OnThreadException;
            syncCtx.Post(async _ =>
            {
                try
                {
                    await action();
                    tcs.TrySetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
                finally
                {
                    Application.ThreadException -= OnThreadException;
                    Application.ExitThread();
                }
            }, null);
            Application.Run();
        })
        {
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return tcs.Task;
    }
}
