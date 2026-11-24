using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Grand_Theft_Auto_V_Platform_Switcher.Models;
using Grand_Theft_Auto_V_Platform_Switcher.Services;
using Grand_Theft_Auto_V_Platform_Switcher.UI;
using Grand_Theft_Auto_V_Platform_Switcher.UI.Controls;
using Grand_Theft_Auto_V_Platform_Switcher.UI.Views;

namespace Grand_Theft_Auto_V_Platform_Switcher;

internal enum AppView
{
    Launch,
    Health,
    Settings
}

public sealed class MainWindow : Form
{
    private readonly AppLogger _logger;
    private readonly SettingsService _settingsService;
    private readonly EditionDetector _detector;
    private readonly ProcessGuard _processGuard;
    private readonly PlatformSwitchService _switchService;
    private readonly BattlEyeService _battlEyeService;
    private readonly RegistryService _registryService;
    private readonly GameLauncherService _launcherService;
    private readonly DoctorService _doctorService;
    private readonly UpdateService _updateService;
    private readonly EpicManifestService _epicManifestService;

    private AppSettings _settings;
    private ThemePalette _palette;
    private AppView _currentView = AppView.Launch;
    private bool _isBusy;
    private bool _isCheckingUpdates;
    private Task? _shownTask;
    private CancellationTokenSource? _refreshCts;
    private VersionSnapshot _latestVersions = new("Unknown", "Not set", "Not set", "Not set", false, false, false, false, false, false);
    private EpicManifestInspection? _latestEpicInspection;
    private readonly HashSet<GameEdition> _epicVerifyPromptShown = new();
    private DateTimeOffset _lastActivatedRefreshUtc = DateTimeOffset.MinValue;
    private bool _isShowingEpicVerifyPrompt;
    private int _lastDoctorErrors;
    private int _lastDoctorWarnings;

    // Shell controls
    private BufferedTableLayoutPanel _root = null!;
    private BufferedTableLayoutPanel _headerPanel = null!;
    private SegmentedControl _segEdition = null!;
    private BufferedFlowLayoutPanel _navFlow = null!;
    private ActionButton _tabLaunch = null!;
    private ActionButton _tabHealth = null!;
    private ActionButton _tabSettings = null!;

    private BufferedPanel _contentHost = null!;
    private LaunchView _launchView = null!;
    private DoctorView _doctorView = null!;
    private SettingsView _settingsView = null!;

    private BufferedTableLayoutPanel _statusPanel = null!;
    private Label _lblStatus = null!;
    private Label _lblMode = null!;
    private readonly ToolTip _toolTip = new();

    public MainWindow(
        AppLogger logger,
        SettingsService settingsService,
        EditionDetector detector,
        ProcessGuard processGuard,
        PlatformSwitchService switchService,
        BattlEyeService battlEyeService,
        RegistryService registryService,
        GameLauncherService launcherService,
        DoctorService doctorService,
        UpdateService updateService,
        EpicManifestService? epicManifestService = null)
    {
        _logger = logger;
        _settingsService = settingsService;
        _detector = detector;
        _processGuard = processGuard;
        _switchService = switchService;
        _battlEyeService = battlEyeService;
        _registryService = registryService;
        _launcherService = launcherService;
        _doctorService = doctorService;
        _updateService = updateService;
        _epicManifestService = epicManifestService ?? new EpicManifestService(processGuard, logger, detector.EpicManifestsDirectory);

        _settings = _settingsService.Load();
        _palette = ThemeManager.GetPalette(_settings.UseDarkTheme);

        InitializeShell();
        ApplyTheme();
        ShowView(AppView.Launch);

        HandleCreated += (_, _) => ThemeManager.ApplyTitleBarTheme(this, _palette.IsDark);
        Shown += async (_, _) => await (_shownTask = RunUiActionAsync(OnWindowShownAsync));
        Activated += async (_, _) => await RunUiActionAsync(OnWindowActivatedAsync);
        SizeChanged += (_, _) => UpdateLayoutConstraints();
        FormClosing += (_, e) =>
        {
            if (_isBusy || _doctorView.IsBusy)
            {
                e.Cancel = true;
                SetStatus("An operation is currently running. Please wait before closing.");
                return;
            }

            _refreshCts?.Cancel();
            _refreshCts?.Dispose();
            _refreshCts = null;
        };
    }

    private EditionSpec CurrentEditionSpec => EditionSpec.FromEdition(_settings.SelectedEdition);
    private EditionSettings CurrentEditionSettings => _settings.GetEditionSettings(_settings.SelectedEdition);

    private void InitializeShell()
    {
        SuspendLayout();
        Text = "Platform Switcher GTA V";
        Icon = AppAssets.LoadAppIcon();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        ClientSize = new Size(730, 370);
        Font = new Font("Segoe UI", 9F);

        _root = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // 1. Single-Row Persistent Command Header (wraps cleanly to 2 rows only under extreme narrow/font scaling)
        _headerPanel = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(10, 5, 10, 5),
            Margin = new Padding(0)
        };
        _headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _headerPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _headerPanel.Paint += (_, e) =>
        {
            using var pen = new Pen(_palette.Border);
            e.Graphics.DrawLine(pen, 0, _headerPanel.Height - 1, _headerPanel.Width, _headerPanel.Height - 1);
        };

        _segEdition = new SegmentedControl("GTA V Legacy", "GTA V Enhanced")
        {
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0)
        };
        _segEdition.SelectedIndex = _settings.SelectedEdition == GameEdition.Enhanced ? 1 : 0;
        _segEdition.SetTooltips(
            _toolTip,
            "Manage Grand Theft Auto V Legacy",
            "Manage Grand Theft Auto V Enhanced");
        _segEdition.SelectedIndexChanged += async (_, idx) =>
            await RunUiActionAsync(() => SelectEditionAsync(idx == 1 ? GameEdition.Enhanced : GameEdition.Legacy));

        _navFlow = new BufferedFlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        _tabLaunch = CreateNavTabButton("Launch", "Switch and play GTA V across launchers", new Padding(0, 0, 4, 0));
        _tabHealth = CreateNavTabButton("Health", "Check folder, launcher backups, anti-cheat, and registry fixes", new Padding(0, 0, 4, 0));
        _tabSettings = CreateNavTabButton("Settings", "Manage game folders, theme, updates, logs, and about", new Padding(0));

        _tabLaunch.Click += (_, _) => ShowView(AppView.Launch);
        _tabHealth.Click += async (_, _) => await RunUiActionAsync(OpenHealthViewAsync);
        _tabSettings.Click += (_, _) => OpenSettingsView();

        _navFlow.Controls.AddRange([_tabLaunch, _tabHealth, _tabSettings]);

        _headerPanel.Controls.Add(_segEdition, 0, 0);
        _headerPanel.Controls.Add(new Panel { Size = new Size(8, 1), Margin = new Padding(0), TabStop = false }, 1, 0);
        _headerPanel.Controls.Add(_navFlow, 2, 0);

        // 2. Content Host & 3 Modular Views
        _contentHost = new BufferedPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        _launchView = new LaunchView(_toolTip);
        _launchView.SetDisableBattlEyeChecked(_settings.DisableBattlEyeOnLaunch);
        _launchView.AutoDetectRequested += async (_, _) =>
            await RunUiActionAsync(() => AutoDetectEditionAsync(CurrentEditionSpec, showMessage: true));
        _launchView.BrowseFolderRequested += async (_, _) =>
            await RunUiActionAsync(() => BrowseGameDirectoryAsync(CurrentEditionSpec));
        _launchView.DisableBattlEyeChanged += async (_, disabled) =>
        {
            _settings.DisableBattlEyeOnLaunch = disabled;
            _settingsService.Save(_settings);
            await RunUiActionAsync(() => RefreshStateAsync());
        };
        _launchView.OpenHealthRequested += async (_, _) => await RunUiActionAsync(OpenHealthViewAsync);
        _launchView.CardPrimaryActionClicked += async (_, platform) =>
            await RunUiActionAsync(() => HandleCardPrimaryActionAsync(platform));
        _launchView.CardShortcutRequested += async (_, platform) =>
            await RunUiActionAsync(() => CreateShortcutForPlatformAsync(platform));
        _launchView.CardRefreshFilesRequested += async (_, platform) =>
            await RunUiActionAsync(() => RefreshSavedFilesForActivePlatformAsync(platform));
        _launchView.CardOpenFolderRequested += (_, _) => OpenCurrentGameFolder();

        _doctorView = new DoctorView(_detector, _doctorService, _switchService, _battlEyeService, _processGuard, _registryService)
        {
            Visible = false
        };
        _doctorView.SettingsChanged += async (_, _) =>
        {
            _settingsService.Save(_settings);
            await RunUiActionAsync(() => RefreshStateAsync());
        };
        _doctorView.ElevationRequested += (_, _) => Application.Exit();
        _doctorView.ReportUpdated += (_, report) =>
        {
            _lastDoctorErrors = report.ErrorCount;
            _lastDoctorWarnings = report.WarningCount;
            UpdateHealthTabAppearance();
        };
        _doctorView.BusyChanged += (_, busy) => UpdateSubViewBusyState(busy);
        _doctorView.StatusMessage += (_, msg) => SetStatus(msg);
        _doctorView.LinkRegistryRequested += async (_, platform) =>
            await RunUiActionAsync(() => RunRegistryActionAsync(platform, link: true));
        _doctorView.UnlinkRegistryRequested += async (_, platform) =>
            await RunUiActionAsync(() => RunRegistryActionAsync(platform, link: false));

        _settingsView = new SettingsView(_logger)
        {
            Visible = false
        };
        _settingsView.ThemeSelected += async (_, useDark) =>
            await RunUiActionAsync(() => SetThemeAsync(useDark));
        _settingsView.CheckUpdatesOnStartupChanged += (_, enabled) =>
        {
            _settings.CheckUpdatesOnStartup = enabled;
            _settingsService.Save(_settings);
            SetStatus(enabled ? "Startup update check enabled." : "Startup update check disabled.");
        };
        _settingsView.DetectFolderRequested += async (_, edition) =>
            await RunUiActionAsync(() => AutoDetectEditionAsync(EditionSpec.FromEdition(edition), showMessage: true));
        _settingsView.GameFolderChanged += async (_, args) =>
            await RunUiActionAsync(() => UpdateEditionFolderAsync(args.Edition, args.Directory));
        _settingsView.EpicAppIdOverrideSaved += (_, appId) =>
        {
            _settings.Enhanced.EpicAppIdOverride = appId;
            _settingsService.Save(_settings);
            SetStatus(string.IsNullOrWhiteSpace(appId)
                ? "Cleared custom Enhanced Epic App ID."
                : "Saved custom Enhanced Epic App ID.");
        };
        _settingsView.CheckUpdatesNowRequested += async (_, _) =>
            await RunUiActionAsync(() => CheckForUpdatesAsync(silentIfUpToDate: false));

        _contentHost.Controls.Add(_launchView);
        _contentHost.Controls.Add(_doctorView);
        _contentHost.Controls.Add(_settingsView);

        // 3. Single-Line Status Bar
        _statusPanel = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(10, 3, 10, 3),
            Margin = new Padding(0)
        };
        _statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _statusPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 18F));
        _statusPanel.Paint += (_, e) =>
        {
            using var pen = new Pen(_palette.Border);
            e.Graphics.DrawLine(pen, 0, 0, _statusPanel.Width, 0);
        };

        _lblStatus = new Label
        {
            Text = "Ready.",
            Font = new Font("Segoe UI", 8.4F),
            Dock = DockStyle.Fill,
            AutoSize = false,
            AutoEllipsis = true,
            UseMnemonic = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 0, 10, 0),
            TabStop = false
        };
        _lblStatus.FontChanged += (_, _) => UpdateStatusBarHeight();

        _lblMode = new Label
        {
            Text = "Standard",
            Font = new Font("Segoe UI", 8.2F),
            AutoSize = true,
            UseMnemonic = false,
            Anchor = AnchorStyles.Right,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(0),
            TabStop = false
        };

        _statusPanel.Controls.Add(_lblStatus, 0, 0);
        _statusPanel.Controls.Add(_lblMode, 1, 0);

        _root.Controls.Add(_headerPanel, 0, 0);
        _root.Controls.Add(_contentHost, 0, 1);
        _root.Controls.Add(_statusPanel, 0, 2);

        Controls.Add(_root);
        ResumeLayout(true);
        UpdateStatusBarHeight();
    }

    private ActionButton CreateNavTabButton(string text, string tooltip, Padding margin)
    {
        var btn = new ActionButton
        {
            Text = text,
            Font = new Font("Segoe UI Semibold", 8.8F),
            Variant = ActionButtonVariant.NavTab,
            AutoSize = true,
            Padding = new Padding(8, 3, 8, 3),
            Margin = margin
        };
        _toolTip.SetToolTip(btn, tooltip);
        return btn;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (!_isBusy && !_doctorView.IsBusy)
        {
            if (keyData == Keys.Escape && _currentView != AppView.Launch)
            {
                ShowView(AppView.Launch);
                return true;
            }

            if (keyData == (Keys.Control | Keys.D1))
            {
                ShowView(AppView.Launch);
                return true;
            }

            if (keyData == (Keys.Control | Keys.D2))
            {
                _ = RunUiActionAsync(OpenHealthViewAsync);
                return true;
            }

            if (keyData == (Keys.Control | Keys.D3))
            {
                OpenSettingsView();
                return true;
            }
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private bool _updatingHeaderLayout;
    private Panel? _headerSpacer;

    internal void UpdateLayoutConstraints()
    {
        if (IsDisposed)
        {
            return;
        }

        UpdateHeaderLayout();
        UpdateStatusBarHeight();
        _launchView.UpdateLayout();
        _doctorView.UpdateLayout();
        _settingsView.UpdateLayout();
    }

    private void UpdateHeaderLayout()
    {
        if (_updatingHeaderLayout || _headerPanel is null || _headerPanel.IsDisposed || _headerPanel.ClientSize.Width <= 0)
        {
            return;
        }

        _updatingHeaderLayout = true;
        try
        {
            var available = _headerPanel.ClientSize.Width - _headerPanel.Padding.Horizontal;
            var leftPref = _segEdition.GetPreferredSize(Size.Empty);
            var rightPref = _navFlow.GetPreferredSize(Size.Empty);
            var needsWrap = leftPref.Width + rightPref.Width + 16 > available;
            var currentlyWrapped = _headerPanel.RowCount == 2;

            if (needsWrap != currentlyWrapped)
            {
                _headerPanel.SuspendLayout();
                _headerPanel.Controls.Clear();
                _headerPanel.ColumnStyles.Clear();
                _headerPanel.RowStyles.Clear();

                if (needsWrap)
                {
                    _headerPanel.ColumnCount = 1;
                    _headerPanel.RowCount = 2;
                    _headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                    _headerPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    _headerPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    _segEdition.Margin = new Padding(0, 0, 0, 4);
                    _navFlow.Anchor = AnchorStyles.Left;
                    _headerPanel.Controls.Add(_segEdition, 0, 0);
                    _headerPanel.Controls.Add(_navFlow, 0, 1);
                }
                else
                {
                    _headerSpacer ??= new Panel { Size = new Size(8, 1), Margin = new Padding(0), TabStop = false };
                    _headerPanel.ColumnCount = 3;
                    _headerPanel.RowCount = 1;
                    _headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                    _headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                    _headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                    _headerPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    _segEdition.Margin = new Padding(0);
                    _navFlow.Anchor = AnchorStyles.Right;
                    _headerPanel.Controls.Add(_segEdition, 0, 0);
                    _headerPanel.Controls.Add(_headerSpacer, 1, 0);
                    _headerPanel.Controls.Add(_navFlow, 2, 0);
                }
                _headerPanel.ResumeLayout(true);
            }

            var headerPref = _headerPanel.GetPreferredSize(new Size(_headerPanel.ClientSize.Width, 0));
            if (Math.Abs(_root.RowStyles[0].Height - headerPref.Height) > 1 ||
                _root.RowStyles[0].SizeType != SizeType.Absolute ||
                Math.Abs(_headerPanel.Height - headerPref.Height) > 1 ||
                _headerPanel.Bottom != _contentHost.Top)
            {
                _root.SuspendLayout();
                _root.RowStyles[0].SizeType = SizeType.Absolute;
                _root.RowStyles[0].Height = headerPref.Height;
                _headerPanel.Height = headerPref.Height;
                _root.ResumeLayout(true);
                _root.PerformLayout(_headerPanel, "Bounds");
                PerformLayout();
            }
        }
        finally
        {
            _updatingHeaderLayout = false;
        }
    }

    private void UpdateStatusBarHeight()
    {
        if (_statusPanel is null || _statusPanel.IsDisposed || _lblStatus.IsDisposed || _statusPanel.RowStyles.Count == 0)
        {
            return;
        }

        var textHeight = Math.Max(
            _lblStatus.Font.Height,
            TextRenderer.MeasureText("Ag", _lblStatus.Font, Size.Empty, TextFormatFlags.SingleLine).Height);
        var singleLineHeight = textHeight + Math.Max(2, (int)Math.Round(2f * (DeviceDpi / 96f)));
        if (Math.Abs(_statusPanel.RowStyles[0].Height - singleLineHeight) > 0.5f ||
            _statusPanel.RowStyles[0].SizeType != SizeType.Absolute)
        {
            _statusPanel.RowStyles[0].SizeType = SizeType.Absolute;
            _statusPanel.RowStyles[0].Height = singleLineHeight;
            _lblStatus.Height = singleLineHeight;
        }
    }

    private void ShowView(AppView view)
    {
        if (_isBusy || _doctorView.IsBusy)
        {
            return;
        }

        _currentView = view;
        _launchView.Visible = view == AppView.Launch;
        _doctorView.Visible = view == AppView.Health;
        _settingsView.Visible = view == AppView.Settings;

        _tabLaunch.Variant = view == AppView.Launch ? ActionButtonVariant.NavTabActive : ActionButtonVariant.NavTab;
        _tabHealth.Variant = view == AppView.Health ? ActionButtonVariant.NavTabActive : ActionButtonVariant.NavTab;
        _tabSettings.Variant = view == AppView.Settings ? ActionButtonVariant.NavTabActive : ActionButtonVariant.NavTab;
        _tabLaunch.ApplyTheme(_palette);
        _tabHealth.ApplyTheme(_palette);
        _tabSettings.ApplyTheme(_palette);

        Control activeView = view switch
        {
            AppView.Health => _doctorView,
            AppView.Settings => _settingsView,
            _ => _launchView
        };
        activeView.BringToFront();

        UpdateLayoutConstraints();
        PerformLayout();
    }

    private async Task OpenHealthViewAsync()
    {
        if (_isBusy || _doctorView.IsBusy)
        {
            return;
        }

        ShowView(AppView.Health);
        await _doctorView.LoadAndRunAsync(CurrentEditionSpec, CurrentEditionSettings);
        if (!IsDisposed)
        {
            UpdateLayoutConstraints();
        }
    }

    private void OpenSettingsView()
    {
        if (_isBusy || _doctorView.IsBusy)
        {
            return;
        }

        _settingsView.LoadFromSettings(_settings);
        ShowView(AppView.Settings);
    }

    private async Task OnWindowShownAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentEditionSettings.GameDirectory))
        {
            await AutoDetectEditionAsync(CurrentEditionSpec, showMessage: false, refreshAfter: false);
        }

        await RefreshStateAsync();

        if (_settings.CheckUpdatesOnStartup && !IsDisposed)
        {
            await CheckForUpdatesAsync(silentIfUpToDate: true);
        }
    }

    private async Task OnWindowActivatedAsync()
    {
        if (_isBusy || _doctorView.IsBusy || _isShowingEpicVerifyPrompt || !Visible || WindowState == FormWindowState.Minimized || Location.X <= -1500)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if ((now - _lastActivatedRefreshUtc).TotalSeconds < 2.5)
        {
            return;
        }

        _lastActivatedRefreshUtc = now;
        await RefreshStateAsync();
    }

    private async Task SelectEditionAsync(GameEdition edition)
    {
        if (_isBusy || _doctorView.IsBusy)
        {
            return;
        }

        _settings.SelectedEdition = edition;
        _segEdition.SelectedIndex = edition == GameEdition.Enhanced ? 1 : 0;

        if (string.IsNullOrWhiteSpace(CurrentEditionSettings.GameDirectory))
        {
            await AutoDetectEditionAsync(CurrentEditionSpec, showMessage: false, refreshAfter: false);
        }

        _settingsService.Save(_settings);
        await RefreshStateAsync();

        if (_currentView == AppView.Health && !IsDisposed)
        {
            await _doctorView.LoadAndRunAsync(CurrentEditionSpec, CurrentEditionSettings);
        }
    }

    private async Task AutoDetectEditionAsync(EditionSpec edition, bool showMessage, bool refreshAfter = true)
    {
        if (_isBusy)
        {
            return;
        }

        var edSettings = _settings.GetEditionSettings(edition.Edition);
        var result = await Task.Run(() => _detector.DetectInstallation(edition));
        if (IsDisposed)
        {
            return;
        }

        if (result.GameDirectory is not null)
        {
            edSettings.GameDirectory = result.GameDirectory;
            var detectedPlatform = await Task.Run(() => _detector.DetectActivePlatform(edition, result.GameDirectory));
            if (IsDisposed)
            {
                return;
            }

            if (detectedPlatform != GamePlatform.Unknown)
            {
                edSettings.CurrentPlatform = detectedPlatform;
            }

            _settingsService.Save(_settings);
            if (_currentView == AppView.Settings)
            {
                _settingsView.LoadFromSettings(_settings);
            }

            if (refreshAfter)
            {
                await RefreshStateAsync();
            }

            SetStatus($"Detected {edition.DisplayName} via {result.Source}.");
            if (showMessage)
            {
                MessageBox.Show(
                    this,
                    $"Found {edition.DisplayName} ({result.Source}):\n{result.GameDirectory}",
                    "Folder Detected",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
        else if (showMessage)
        {
            MessageBox.Show(
                this,
                $"Could not auto-detect {edition.DisplayName}. Click Change... to choose the game folder manually.",
                "Auto-Detect",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private async Task BrowseGameDirectoryAsync(EditionSpec edition)
    {
        if (_isBusy)
        {
            return;
        }

        var edSettings = _settings.GetEditionSettings(edition.Edition);
        using var dialog = new FolderBrowserDialog
        {
            Description = $"Select folder containing {edition.MainExecutable}",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(edSettings.GameDirectory) ? edSettings.GameDirectory : string.Empty
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await UpdateEditionFolderAsync(edition.Edition, dialog.SelectedPath);
    }

    private async Task UpdateEditionFolderAsync(GameEdition editionEnum, string folderPath)
    {
        var edition = EditionSpec.FromEdition(editionEnum);
        var edSettings = _settings.GetEditionSettings(editionEnum);
        edSettings.GameDirectory = folderPath.Trim();
        edSettings.CurrentPlatform = await Task.Run(() => _detector.DetectActivePlatform(edition, edSettings.GameDirectory));
        if (IsDisposed)
        {
            return;
        }

        _settingsService.Save(_settings);
        if (_currentView == AppView.Settings)
        {
            _settingsView.LoadFromSettings(_settings);
        }

        await RefreshStateAsync();
        SetStatus($"Updated {edition.DisplayName} folder to {edSettings.GameDirectory}.");
    }

    private async Task HandleCardPrimaryActionAsync(GamePlatform platform)
    {
        if (_isBusy)
        {
            return;
        }

        var edition = CurrentEditionSpec;
        var gameDir = CurrentEditionSettings.GameDirectory;
        var isValidDir = await Task.Run(() => _detector.IsValidGameDirectory(edition, gameDir));
        if (IsDisposed)
        {
            return;
        }

        if (!isValidDir)
        {
            MessageBox.Show(
                this,
                $"Please select a valid {edition.DisplayName} folder first.",
                "Game Folder Needed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        var activePlatform = CurrentEditionSettings.CurrentPlatform;
        if (activePlatform == platform)
        {
            if (platform == GamePlatform.EGL && !HasValidProfile(_latestVersions, GamePlatform.EGL))
            {
                await RefreshSavedFilesForActivePlatformAsync(GamePlatform.EGL);
                return;
            }

            LaunchCurrentGame(platform);
            return;
        }

        var hasProfile = HasValidProfile(_latestVersions, platform);
        var isInvalid = platform == GamePlatform.EGL && _latestVersions.IsEglProfileInvalid;
        if (hasProfile && !isInvalid)
        {
            await SwitchToPlatformAsync(platform, launchAfter: false);
            return;
        }

        if (platform == GamePlatform.EGL)
        {
            await HandleEpicSetupFromCardAsync(edition, gameDir, isInvalid);
            return;
        }

        using var guideDialog = new AddLauncherGuideDialog(edition, platform, gameDir, _palette);
        guideDialog.ShowDialog(this);
    }

    private async Task SwitchToPlatformAsync(GamePlatform targetPlatform, bool launchAfter = false)
    {
        if (_isBusy)
        {
            return;
        }

        var edition = CurrentEditionSpec;
        var gameDir = CurrentEditionSettings.GameDirectory;
        var platformDisplayName = EditionSpec.GetPlatformDisplayName(targetPlatform);

        var isValidDir = await Task.Run(() => _detector.IsValidGameDirectory(edition, gameDir));
        if (IsDisposed)
        {
            return;
        }

        if (!isValidDir)
        {
            MessageBox.Show(this, $"Please select a valid {edition.DisplayName} game folder first.",
                "Game Folder Needed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Step 0: Automatically preserve active launcher files if missing or outdated before overwriting.
        var detectedActive = await Task.Run(() => _detector.DetectActivePlatform(edition, gameDir));
        if (IsDisposed)
        {
            return;
        }

        var currentActive = detectedActive != GamePlatform.Unknown ? detectedActive : CurrentEditionSettings.CurrentPlatform;
        if (currentActive != GamePlatform.Unknown && currentActive != targetPlatform)
        {
            var preSwitchSnapshot = await Task.Run(() => _switchService.GetVersionSnapshot(edition, gameDir));
            if (IsDisposed)
            {
                return;
            }

            if (NeedsProfileCapture(preSwitchSnapshot, currentActive))
            {
                var sigCheck = await Task.Run(() => PlatformSwitchService.ValidateActiveFolderForBackup(gameDir, currentActive));
                if (IsDisposed)
                {
                    return;
                }

                if (sigCheck.Success)
                {
                    SetBusy(true);
                    try
                    {
                        var progress = new Progress<string>(SetStatus);
                        var saveRes = await _switchService.BackupCurrentPlatformAsync(edition, gameDir, currentActive, progress);
                        if (IsDisposed)
                        {
                            return;
                        }

                        if (!saveRes.Success)
                        {
                            if (saveRes.RequiresElevation)
                            {
                                PromptElevation(saveRes.Message);
                            }
                            else
                            {
                                MessageBox.Show(
                                    this,
                                    $"Could not save your current {EditionSpec.GetPlatformDisplayName(currentActive)} files before switching:\n{saveRes.Message}",
                                    "Safety Backup Failed",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning);
                            }
                            return;
                        }
                    }
                    finally
                    {
                        if (!IsDisposed)
                        {
                            SetBusy(false);
                        }
                    }
                }
            }
        }

        // Version Mismatch Guard: warn if target launcher files are older than active game exe.
        var mismatch = await Task.Run(() => _switchService.CheckProfileVersionMismatch(edition, gameDir, targetPlatform));
        if (IsDisposed)
        {
            return;
        }

        if (mismatch.IsOutdated)
        {
            var answer = MessageBox.Show(
                this,
                $"Saved {platformDisplayName} files (v{mismatch.ProfileVersion}) are older than your current game (v{mismatch.ActiveVersion}).\n\n" +
                "Switching now may trigger a launcher update download.\n\nContinue anyway?",
                "Older Launcher Files",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (answer != DialogResult.Yes)
            {
                SetStatus("Switch cancelled.");
                return;
            }
        }

        var switchedOk = false;
        SetBusy(true);
        try
        {
            var progress = new Progress<string>(SetStatus);
            var result = await _switchService.SwitchAsync(edition, gameDir, targetPlatform, progress);
            if (IsDisposed)
            {
                return;
            }

            if (result.Success)
            {
                switchedOk = true;
                CurrentEditionSettings.PreviousPlatform = CurrentEditionSettings.CurrentPlatform;
                CurrentEditionSettings.CurrentPlatform = targetPlatform;
                _settingsService.Save(_settings);

                await Task.Run(() =>
                {
                    if (_registryService.IsAdministrator())
                    {
                        try
                        {
                            if (targetPlatform == GamePlatform.Steam)
                            {
                                _registryService.AddPlayOnSteam(edition, gameDir);
                            }
                            else if (targetPlatform == GamePlatform.EGL)
                            {
                                _registryService.AddPlayOnEpic(edition, gameDir);
                            }
                            else if (targetPlatform == GamePlatform.RGL)
                            {
                                _registryService.SyncPlayOnRgl(edition, gameDir);
                            }
                        }
                        catch (Exception regEx)
                        {
                            _logger.Warn($"Automatic registry sync after switching to {targetPlatform} failed: {regEx.Message}");
                        }
                    }

                    if (targetPlatform == GamePlatform.EGL && !_processGuard.IsEpicGamesLauncherRunning())
                    {
                        _epicManifestService.SyncManifestPath(edition, gameDir, CurrentEditionSettings.EpicAppIdOverride);
                    }
                });

                if (IsDisposed)
                {
                    return;
                }

                SetStatus(result.Message);
            }
            else
            {
                SetStatus("Switch failed.");
                if (result.RequiresElevation)
                {
                    PromptElevation(result.Message);
                }
                else
                {
                    MessageBox.Show(this, result.Message, "Switch Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        finally
        {
            if (!IsDisposed)
            {
                SetBusy(false);
                await RefreshStateAsync();
            }
        }

        if (switchedOk && launchAfter && !IsDisposed)
        {
            LaunchCurrentGame(targetPlatform);
        }
    }

    private void PromptElevation(string message)
    {
        var elevate = MessageBox.Show(
            this,
            message + "\n\nRestart as Administrator now?",
            "Administrator Permission Required",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (elevate == DialogResult.Yes && _registryService.RestartAsAdministrator())
        {
            Application.Exit();
        }
    }

    private async Task RefreshSavedFilesForActivePlatformAsync(GamePlatform platform)
    {
        if (_isBusy)
        {
            return;
        }

        var edition = CurrentEditionSpec;
        var gameDir = CurrentEditionSettings.GameDirectory;
        var sigCheck = await Task.Run(() => PlatformSwitchService.ValidateActiveFolderForBackup(gameDir, platform));
        if (IsDisposed)
        {
            return;
        }

        if (!sigCheck.Success)
        {
            MessageBox.Show(this, sigCheck.Message, "Active Launcher Mismatch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SetBusy(true);
        try
        {
            var progress = new Progress<string>(SetStatus);
            var result = await _switchService.BackupCurrentPlatformAsync(edition, gameDir, platform, progress);
            if (IsDisposed)
            {
                return;
            }

            if (result.Success)
            {
                SetStatus($"Refreshed saved {EditionSpec.GetPlatformDisplayName(platform)} files.");
            }
            else if (result.RequiresElevation)
            {
                PromptElevation(result.Message);
            }
            else
            {
                MessageBox.Show(this, result.Message, "Refresh Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        finally
        {
            if (!IsDisposed)
            {
                SetBusy(false);
                await RefreshStateAsync();
            }
        }
    }

    private async Task HandleEpicSetupFromCardAsync(EditionSpec edition, string gameDir, bool isInvalidBackup)
    {
        if (isInvalidBackup)
        {
            var cleanPrompt = MessageBox.Show(
                this,
                "Your saved Epic Games folder is missing required Epic files and cannot be used.\n\nRemove this broken Epic backup now?",
                "Fix Epic Setup",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (cleanPrompt != DialogResult.Yes)
            {
                return;
            }

            SetBusy(true);
            try
            {
                var cleanRes = await Task.Run(() => _switchService.CleanInvalidProfile(edition, gameDir, GamePlatform.EGL));
                if (IsDisposed)
                {
                    return;
                }

                SetStatus(cleanRes.Message);
                if (!cleanRes.Success)
                {
                    MessageBox.Show(this, cleanRes.Message, "Fix Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            finally
            {
                if (!IsDisposed)
                {
                    SetBusy(false);
                    await RefreshStateAsync();
                }
            }
        }

        var inspection = await Task.Run(() => _epicManifestService.Inspect(edition, gameDir, CurrentEditionSettings.EpicAppIdOverride));
        if (IsDisposed)
        {
            return;
        }

        if (inspection.State == EpicManifestState.Aligned && !PlatformSwitchService.HasActiveEpicSdk(gameDir))
        {
            var relinkChoice = MessageBox.Show(
                this,
                $"Epic Games Launcher is already linked to your {edition.DisplayName} folder:\n{gameDir}\n\n" +
                "Next steps to finish setup:\n" +
                $"1. Open Epic Games Launcher and click Resume / Verify on {edition.DisplayName}.\n" +
                "2. Wait for Epic Games to verify your existing files and install its launcher files.\n" +
                "3. Return to this window — your Epic Games setup will be detected automatically.\n\n" +
                "Do you want to re-link from a different temporary '.egstore' folder instead?",
                "Awaiting Epic Games Verification",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button2);

            if (relinkChoice != DialogResult.Yes)
            {
                return;
            }
        }
        else if (inspection.State == EpicManifestState.PointsElsewhere ||
                 (inspection.State == EpicManifestState.AlignedMissingEgstore && inspection.HasSourceEgstore))
        {
            var confirm = MessageBox.Show(
                this,
                $"Epic Games Launcher was found at:\n{inspection.CurrentInstallLocation}\n\n" +
                $"Link your existing GTA V folder ({gameDir}) to Epic Games now?\n\n" +
                "Your current launcher files will be saved automatically first.",
                "Link Epic Games Folder",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
            {
                return;
            }

            SetBusy(true);
            try
            {
                var progress = new Progress<string>(SetStatus);
                var adoptResult = await _epicManifestService.RelocateAndAdoptAsync(
                    edition,
                    gameDir,
                    _switchService,
                    CurrentEditionSettings.EpicAppIdOverride,
                    progress);
                if (IsDisposed)
                {
                    return;
                }

                SetStatus(adoptResult.Success ? "Epic Games folder linked." : "Epic Games setup failed.");
                MessageBox.Show(
                    this,
                    adoptResult.Message,
                    adoptResult.Success ? "Epic Games Linked" : "Setup Failed",
                    MessageBoxButtons.OK,
                    adoptResult.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            finally
            {
                if (!IsDisposed)
                {
                    SetBusy(false);
                    await RefreshStateAsync();
                }
            }

            return;
        }

        using var setupDialog = new EpicSetupDialog(edition, _processGuard, _palette);
        if (setupDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        SetBusy(true);
        try
        {
            var progress = new Progress<string>(SetStatus);
            var adoptResult = await _epicManifestService.RelocateAndAdoptFromFolderAsync(
                edition,
                gameDir,
                setupDialog.SelectedTempFolder,
                _switchService,
                CurrentEditionSettings.EpicAppIdOverride,
                progress);
            if (IsDisposed)
            {
                return;
            }

            SetStatus(adoptResult.Success ? "Epic Games folder linked." : "Epic Games setup failed.");
            MessageBox.Show(
                this,
                adoptResult.Message,
                adoptResult.Success ? "Epic Games Linked" : "Setup Failed",
                MessageBoxButtons.OK,
                adoptResult.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        finally
        {
            if (!IsDisposed)
            {
                SetBusy(false);
                await RefreshStateAsync();
            }
        }
    }

    private async Task CreateShortcutForPlatformAsync(GamePlatform platform)
    {
        if (_isBusy)
        {
            return;
        }

        var platformName = EditionSpec.GetPlatformDisplayName(platform);
        SetBusy(true);
        SetStatus($"Creating desktop shortcut for {platformName}...");
        try
        {
            var result = await _launcherService.CreateDesktopShortcutAsync(
                CurrentEditionSpec,
                platform,
                _settings.DisableBattlEyeOnLaunch);
            if (IsDisposed)
            {
                return;
            }

            SetStatus(result.Success ? $"Desktop shortcut created for {platformName}." : "Shortcut creation failed.");
            if (!result.Success)
            {
                MessageBox.Show(this, result.Message, "Shortcut Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        finally
        {
            if (!IsDisposed)
            {
                SetBusy(false);
                await RefreshStateAsync();
            }
        }
    }

    private void LaunchCurrentGame(GamePlatform platform)
    {
        if (_isBusy)
        {
            return;
        }

        var result = _launcherService.Launch(
            CurrentEditionSpec,
            CurrentEditionSettings.GameDirectory,
            platform,
            _settings.DisableBattlEyeOnLaunch,
            CurrentEditionSettings.EpicAppIdOverride);

        if (result.Success)
        {
            var statusNote = _settings.DisableBattlEyeOnLaunch && platform == GamePlatform.EGL
                ? $"Launched {CurrentEditionSpec.DisplayName} via Epic Games — turn off BattlEye inside Rockstar Launcher settings for mods."
                : $"Launched {CurrentEditionSpec.DisplayName} via {EditionSpec.GetPlatformDisplayName(platform)}.";
            SetStatus(statusNote);
        }
        else
        {
            MessageBox.Show(this, result.Message, "Launch Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OpenCurrentGameFolder()
    {
        var dir = CurrentEditionSettings.GameDirectory;
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Unable to open folder: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task RunRegistryActionAsync(GamePlatform platform, bool link)
    {
        if (_isBusy || _doctorView.IsBusy)
        {
            return;
        }

        var edition = CurrentEditionSpec;
        var gameDir = CurrentEditionSettings.GameDirectory;
        var isValid = await Task.Run(() => _detector.IsValidGameDirectory(edition, gameDir));
        if (IsDisposed)
        {
            return;
        }

        if (!isValid)
        {
            MessageBox.Show(
                this,
                $"Please select a valid {edition.DisplayName} game folder before changing registry settings.",
                "Game Folder Needed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        try
        {
            await Task.Run(() =>
            {
                if (platform == GamePlatform.Steam)
                {
                    if (link) _registryService.AddPlayOnSteam(edition, gameDir);
                    else _registryService.RemovePlayOnSteam(edition);
                }
                else if (platform == GamePlatform.EGL)
                {
                    if (link) _registryService.AddPlayOnEpic(edition, gameDir);
                    else _registryService.RemovePlayOnEpic(edition);
                }
            });

            if (IsDisposed)
            {
                return;
            }

            var actionLabel = link ? "Linked" : "Unlinked";
            var platformLabel = EditionSpec.GetPlatformDisplayName(platform);
            var msg = $"{actionLabel} {platformLabel} in Rockstar registry. Restart Rockstar Launcher to apply.";
            SetStatus(msg);
            if (_currentView == AppView.Health)
            {
                await _doctorView.LoadAndRunAsync(edition, CurrentEditionSettings);
            }
            else
            {
                await RefreshStateAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Registry fix failed.", ex);
            MessageBox.Show(this, $"Registry update failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task CheckForUpdatesAsync(bool silentIfUpToDate)
    {
        if (_isCheckingUpdates || _isBusy)
        {
            return;
        }

        _isCheckingUpdates = true;
        _settingsView.SetUpdateStatus("Checking...", StatusTone.Accent, checking: true);
        if (!silentIfUpToDate)
        {
            SetStatus("Checking GitHub Releases for updates...");
        }

        try
        {
            var result = await _updateService.CheckForUpdatesAsync();
            if (IsDisposed)
            {
                return;
            }

            if (!result.Success)
            {
                _settingsView.SetUpdateStatus("Check failed", StatusTone.Warning, checking: false);
                if (!silentIfUpToDate)
                {
                    SetStatus("Update check failed.");
                    MessageBox.Show(this, result.Message, "Update Check", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return;
            }

            if (result.UpdateAvailable)
            {
                _settingsView.SetUpdateStatus($"Update v{result.LatestVersion} available", StatusTone.Accent, checking: false);
                SetStatus($"Update available: v{result.LatestVersion}");
                var answer = MessageBox.Show(
                    this,
                    result.Message + "\n\nOpen the GitHub release page now?",
                    "Update Available",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);
                if (answer == DialogResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(result.ReleaseUrl) { UseShellExecute = true });
                }
            }
            else
            {
                _settingsView.SetUpdateStatus($"Up to date (v{result.CurrentVersion})", StatusTone.Success, checking: false);
                if (!silentIfUpToDate)
                {
                    SetStatus(result.Message);
                    MessageBox.Show(this, result.Message, "Up to Date", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
        finally
        {
            _isCheckingUpdates = false;
            if (!IsDisposed)
            {
                _settingsView.SetBusy(_isBusy);
            }
        }
    }

    private async Task SetThemeAsync(bool useDarkTheme)
    {
        if (_isBusy || _doctorView.IsBusy || _settings.UseDarkTheme == useDarkTheme)
        {
            return;
        }

        _settings.UseDarkTheme = useDarkTheme;
        _settingsService.Save(_settings);
        _palette = ThemeManager.GetPalette(useDarkTheme);
        ApplyTheme();
        ThemeManager.ApplyTitleBarTheme(this, _palette.IsDark);
        await RefreshStateAsync();
    }

    private async Task<DoctorReport?> RefreshStateAsync()
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        var cts = new CancellationTokenSource();
        _refreshCts = cts;
        var token = cts.Token;

        var edition = CurrentEditionSpec;
        var edSettings = CurrentEditionSettings;
        var edSnapshot = new EditionSettings
        {
            GameDirectory = edSettings.GameDirectory,
            CurrentPlatform = edSettings.CurrentPlatform,
            PreviousPlatform = edSettings.PreviousPlatform,
            EpicAppIdOverride = edSettings.EpicAppIdOverride,
            FirstRunDoctorShown = edSettings.FirstRunDoctorShown
        };

        (bool isValidDir, GamePlatform detectedPlatform, bool isAwaitingEpicVerify, bool hasActiveEpicSdk, VersionSnapshot versions, BattlEyeStatus beStatus, bool isAdmin, EpicManifestInspection? epicInspection, DoctorReport report) snapshot;
        try
        {
            snapshot = await Task.Run(async () =>
            {
                var valid = _detector.IsValidGameDirectory(edition, edSnapshot.GameDirectory);
                var epic = valid ? _epicManifestService.Inspect(edition, edSnapshot.GameDirectory, edSnapshot.EpicAppIdOverride) : null;
                var v = _switchService.GetVersionSnapshot(edition, edSnapshot.GameDirectory);

                var activeEos = valid && PlatformSwitchService.HasActiveEpicSdk(edSnapshot.GameDirectory);
                var parkedEos = valid && PlatformSwitchService.HasParkedEpicSdk(edSnapshot.GameDirectory);
                var parkedSteam = valid && File.Exists(Path.Combine(edSnapshot.GameDirectory, "steam_api64.dll.steam"));
                var awaitingVerify = valid &&
                                     epic?.State == EpicManifestState.Aligned &&
                                     !activeEos &&
                                     !parkedEos &&
                                     !v.HasEglProfile;

                var detected = valid ? _detector.DetectActivePlatform(edition, edSnapshot.GameDirectory) : GamePlatform.Unknown;
                if (awaitingVerify && detected == GamePlatform.RGL && parkedSteam && !v.HasRglProfile)
                {
                    // Do not misclassify parked Steam files as Rockstar Games while awaiting first Epic verification.
                    detected = GamePlatform.Unknown;
                    edSnapshot.CurrentPlatform = GamePlatform.Unknown;
                }
                else if (detected != GamePlatform.Unknown)
                {
                    edSnapshot.CurrentPlatform = detected;
                }

                // For first-time Epic post-verify (Option A), let the user confirm or click Save Epic Files instead of silent background capture.
                var isFirstEpicPostVerify = detected == GamePlatform.EGL && (!v.HasEglProfile || v.IsEglProfileInvalid);

                // Silent auto-capture for the active platform if missing or outdated and safe to write.
                if (valid &&
                    detected != GamePlatform.Unknown &&
                    !awaitingVerify &&
                    !isFirstEpicPostVerify &&
                    !_isBusy &&
                    _processGuard.GetBlockingProcesses(edition).Count == 0 &&
                    _switchService.CanWriteToDirectory(edSnapshot.GameDirectory) &&
                    NeedsProfileCapture(v, detected) &&
                    PlatformSwitchService.ValidateActiveFolderForBackup(edSnapshot.GameDirectory, detected).Success)
                {
                    await _switchService.BackupCurrentPlatformAsync(
                        edition,
                        edSnapshot.GameDirectory,
                        detected,
                        cancellationToken: token).ConfigureAwait(false);
                    v = _switchService.GetVersionSnapshot(edition, edSnapshot.GameDirectory);
                }

                var be = _battlEyeService.Inspect(edition, edSnapshot.GameDirectory);
                var admin = _registryService.IsAdministrator();
                var doc = _doctorService.Diagnose(edition, edSnapshot);
                return (valid, detected, awaitingVerify, activeEos, v, be, admin, epic, doc);
            }, token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        if (token.IsCancellationRequested || IsDisposed)
        {
            return null;
        }

        var (isValidDir, detectedPlatform, isAwaitingEpicVerify, hasActiveEpicSdk, versions, beStatus, isAdmin, epicInspection, report) = snapshot;

        if (isValidDir)
        {
            if (isAwaitingEpicVerify && edSettings.CurrentPlatform != GamePlatform.Unknown && !versions.HasRglProfile &&
                File.Exists(Path.Combine(edSettings.GameDirectory, "steam_api64.dll.steam")))
            {
                edSettings.CurrentPlatform = GamePlatform.Unknown;
                _settingsService.Save(_settings);
            }
            else if (detectedPlatform != GamePlatform.Unknown && edSettings.CurrentPlatform != detectedPlatform)
            {
                edSettings.CurrentPlatform = detectedPlatform;
                _settingsService.Save(_settings);
            }
        }

        _latestVersions = versions;
        _latestEpicInspection = epicInspection;

        var activePlatform = isValidDir ? edSettings.CurrentPlatform : GamePlatform.Unknown;
        var rglModel = BuildCardModel(GamePlatform.RGL, "Rockstar Games", versions.RglProfileVersion, versions.HasRglProfile, versions.IsRglOutdated, false, activePlatform, isValidDir, null, hasActiveEpicSdk: false);
        var steamModel = BuildCardModel(GamePlatform.Steam, "Steam", versions.SteamProfileVersion, versions.HasSteamProfile, versions.IsSteamOutdated, false, activePlatform, isValidDir, null, hasActiveEpicSdk: false);
        var eglModel = BuildCardModel(GamePlatform.EGL, "Epic Games", versions.EglProfileVersion, versions.HasEglProfile, versions.IsEglOutdated, versions.IsEglProfileInvalid, activePlatform, isValidDir, epicInspection, hasActiveEpicSdk);

        var (beText, beTone) = ResolveBattlEyePill(isValidDir, beStatus, activePlatform, _settings.DisableBattlEyeOnLaunch);

        _lastDoctorErrors = report.ErrorCount;
        _lastDoctorWarnings = report.WarningCount;

        _launchView.BindState(
            edSettings.GameDirectory,
            isValidDir,
            versions.ActiveGameVersion,
            rglModel,
            steamModel,
            eglModel,
            beText,
            beTone,
            beStatus.Summary,
            report.ErrorCount,
            report.WarningCount);

        if (_currentView != AppView.Health)
        {
            _doctorView.UpdateFromReport(edition, edSettings, report);
        }

        _lblMode.Text = isAdmin ? "Admin" : "Standard";
        UpdateHealthTabAppearance();
        UpdateLayoutConstraints();

        // Option A: Prompt once per edition per session when Epic verification finishes and Epic SDK files are newly active without a saved backup.
        if (isValidDir &&
            activePlatform == GamePlatform.EGL &&
            hasActiveEpicSdk &&
            (!versions.HasEglProfile || versions.IsEglProfileInvalid) &&
            epicInspection?.State == EpicManifestState.Aligned &&
            Visible &&
            Location.X > -1500 &&
            !_isBusy &&
            !_doctorView.IsBusy &&
            !_isShowingEpicVerifyPrompt &&
            _epicVerifyPromptShown.Add(edition.Edition))
        {
            _isShowingEpicVerifyPrompt = true;
            try
            {
                SetStatus($"Epic Games verification detected for {edition.DisplayName}. Save your Epic backup to finish setup.");
                var saveNow = MessageBox.Show(
                    this,
                    $"Epic Games verification is complete! Epic launcher files were detected in your {edition.DisplayName} folder.\n\n" +
                    "Save your Epic Games backup now so you can switch freely between launchers?",
                    "Epic Games Ready",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (saveNow == DialogResult.Yes && !IsDisposed)
                {
                    await RefreshSavedFilesForActivePlatformAsync(GamePlatform.EGL);
                    return _doctorView is not null && !IsDisposed
                        ? _doctorService.Diagnose(CurrentEditionSpec, CurrentEditionSettings)
                        : report;
                }
            }
            finally
            {
                _isShowingEpicVerifyPrompt = false;
            }
        }

        return report;
    }

    private static (string Text, StatusTone Tone) ResolveBattlEyePill(
        bool isValidDir,
        BattlEyeStatus beStatus,
        GamePlatform activePlatform,
        bool disableBattlEye)
    {
        if (!isValidDir)
        {
            return ("BattlEye: Select folder", StatusTone.Neutral);
        }

        if (beStatus.IsHealthy && activePlatform == GamePlatform.EGL && disableBattlEye)
        {
            return ("Turn off in Rockstar Launcher too", StatusTone.Warning);
        }

        return beStatus.IsHealthy
            ? ("BattlEye Ready", StatusTone.Success)
            : ("BattlEye Missing", StatusTone.Warning);
    }

    private static string FormatSavedVersion(string profileVersion)
    {
        if (string.IsNullOrWhiteSpace(profileVersion) ||
            profileVersion.Equals("Present", StringComparison.OrdinalIgnoreCase) ||
            profileVersion.Equals("Unknown", StringComparison.OrdinalIgnoreCase) ||
            profileVersion.Equals("Not set", StringComparison.OrdinalIgnoreCase))
        {
            return "Saved locally";
        }

        return char.IsDigit(profileVersion[0]) ? $"Saved: v{profileVersion}" : $"Saved: {profileVersion}";
    }

    private static bool HasValidProfile(VersionSnapshot versions, GamePlatform platform) => platform switch
    {
        GamePlatform.RGL => versions.HasRglProfile,
        GamePlatform.Steam => versions.HasSteamProfile,
        GamePlatform.EGL => versions.HasEglProfile && !versions.IsEglProfileInvalid,
        _ => false
    };

    private static bool NeedsProfileCapture(VersionSnapshot versions, GamePlatform platform) => platform switch
    {
        GamePlatform.RGL => !versions.HasRglProfile || versions.IsRglOutdated,
        GamePlatform.Steam => !versions.HasSteamProfile || versions.IsSteamOutdated,
        GamePlatform.EGL => !versions.HasEglProfile || versions.IsEglProfileInvalid || versions.IsEglOutdated,
        _ => false
    };

    private static PlatformCardModel BuildCardModel(
        GamePlatform platform,
        string displayName,
        string profileVersion,
        bool hasProfile,
        bool isOutdated,
        bool isInvalidProfile,
        GamePlatform activePlatform,
        bool isValidDir,
        EpicManifestInspection? epicInspection,
        bool hasActiveEpicSdk = false)
    {
        var isActive = activePlatform == platform;

        // Option A Post-Verify Ready State: Epic SDK is active in game folder, ready for 1-click backup save (even if an old broken backup existed).
        if (platform == GamePlatform.EGL && isActive && hasActiveEpicSdk && (!hasProfile || isInvalidProfile))
        {
            return new PlatformCardModel(
                platform,
                displayName,
                "Epic ready",
                StatusTone.Success,
                "Verified in Epic",
                "Save Epic backup now",
                StatusTone.Accent,
                IsActive: true,
                IsReady: true,
                PrimaryActionText: "Save Epic Files",
                PrimaryVariant: ActionButtonVariant.PrimaryAccent,
                PrimaryEnabled: isValidDir,
                ShowOverflowMenu: isValidDir,
                CanCreateShortcut: false,
                CanRefreshFiles: isValidDir,
                CanOpenFolder: isValidDir,
                StatusTooltip: "Epic Games verification succeeded! Click Save Epic Files to store your Epic launcher backup.",
                PrimaryTooltip: "Save Epic Games launcher files to finish setup");
        }

        if (platform == GamePlatform.EGL && isInvalidProfile)
        {
            return new PlatformCardModel(
                platform,
                displayName,
                "Needs repair",
                StatusTone.Danger,
                "Invalid Epic backup",
                "Clean before linking",
                StatusTone.Danger,
                IsActive: isActive,
                IsReady: false,
                PrimaryActionText: "Fix Epic Setup",
                PrimaryVariant: ActionButtonVariant.Danger,
                PrimaryEnabled: isValidDir,
                ShowOverflowMenu: isValidDir,
                CanCreateShortcut: false,
                CanRefreshFiles: false,
                CanOpenFolder: isValidDir,
                StatusTooltip: "Saved Epic Games backup is missing required Epic SDK files.",
                PrimaryTooltip: "Remove broken Epic backup and link Epic Games");
        }

        if (isActive)
        {
            var statusText = isOutdated ? "Game updated" : "Active";
            var statusTone = isOutdated ? StatusTone.Warning : StatusTone.Accent;
            var verText = hasProfile ? FormatSavedVersion(profileVersion) : "Files not saved yet";
            var hintText = isOutdated
                ? "Refresh saved files"
                : hasProfile
                    ? "Current launcher"
                    : "Will save automatically";

            return new PlatformCardModel(
                platform,
                displayName,
                statusText,
                statusTone,
                verText,
                hintText,
                isOutdated ? StatusTone.Warning : StatusTone.Neutral,
                IsActive: true,
                IsReady: true,
                PrimaryActionText: "Play Now",
                PrimaryVariant: ActionButtonVariant.PrimarySuccess,
                PrimaryEnabled: isValidDir,
                ShowOverflowMenu: isValidDir,
                CanCreateShortcut: isValidDir && hasProfile,
                CanRefreshFiles: isValidDir,
                CanOpenFolder: isValidDir,
                StatusTooltip: isOutdated
                    ? "Your game updated recently. Use '•••' → 'Refresh saved files' or it will save automatically before switching."
                    : "This launcher is currently active.",
                PrimaryTooltip: $"Launch {displayName}");
        }

        if (hasProfile)
        {
            var statusText = isOutdated ? "Outdated files" : "Ready";
            var statusTone = isOutdated ? StatusTone.Warning : StatusTone.Success;
            var verText = FormatSavedVersion(profileVersion);
            var hintText = isOutdated ? "Older game update" : "Matches game version";

            return new PlatformCardModel(
                platform,
                displayName,
                statusText,
                statusTone,
                verText,
                hintText,
                isOutdated ? StatusTone.Warning : StatusTone.Neutral,
                IsActive: false,
                IsReady: true,
                PrimaryActionText: "Switch",
                PrimaryVariant: ActionButtonVariant.Secondary,
                PrimaryEnabled: isValidDir,
                ShowOverflowMenu: isValidDir,
                CanCreateShortcut: isValidDir,
                CanRefreshFiles: false,
                CanOpenFolder: isValidDir,
                StatusTooltip: isOutdated
                    ? "Saved launcher files are from an older game update."
                    : "Ready to switch to this launcher.",
                PrimaryTooltip: $"Switch to {displayName}");
        }

        if (platform == GamePlatform.EGL)
        {
            if (epicInspection?.State == EpicManifestState.Aligned && !hasActiveEpicSdk)
            {
                return new PlatformCardModel(
                    platform,
                    displayName,
                    "Verify in Epic",
                    StatusTone.Warning,
                    "Folder linked",
                    "Resume in Epic Launcher",
                    StatusTone.Warning,
                    IsActive: false,
                    IsReady: false,
                    PrimaryActionText: "Verify Guide",
                    PrimaryVariant: ActionButtonVariant.PrimaryAccent,
                    PrimaryEnabled: isValidDir,
                    ShowOverflowMenu: isValidDir,
                    CanCreateShortcut: false,
                    CanRefreshFiles: false,
                    CanOpenFolder: isValidDir,
                    StatusTooltip: "Epic Games Launcher is linked to this folder. Open Epic Games Launcher and click Resume / Verify to download Epic launcher files.",
                    PrimaryTooltip: "View next steps to finish verifying in Epic Games Launcher");
            }

            var hasDiscoveredManifest = epicInspection is not null &&
                (epicInspection.State == EpicManifestState.PointsElsewhere ||
                 (epicInspection.State == EpicManifestState.AlignedMissingEgstore && epicInspection.HasSourceEgstore));

            return new PlatformCardModel(
                platform,
                displayName,
                hasDiscoveredManifest ? "Found install" : "Not set up",
                hasDiscoveredManifest ? StatusTone.Accent : StatusTone.Neutral,
                "No download needed",
                hasDiscoveredManifest ? "Ready to link folder" : "Link existing folder",
                hasDiscoveredManifest ? StatusTone.Accent : StatusTone.Neutral,
                IsActive: false,
                IsReady: false,
                PrimaryActionText: hasDiscoveredManifest ? "Link Epic Folder" : "Set Up Epic...",
                PrimaryVariant: ActionButtonVariant.Secondary,
                PrimaryEnabled: isValidDir,
                ShowOverflowMenu: isValidDir,
                CanCreateShortcut: false,
                CanRefreshFiles: false,
                CanOpenFolder: isValidDir,
                StatusTooltip: "Adopt your existing GTA V folder for Epic Games without re-downloading.",
                PrimaryTooltip: hasDiscoveredManifest
                    ? "Link Epic Games Launcher to this GTA V folder"
                    : "Open 3-step Epic Games setup guide");
        }

        return new PlatformCardModel(
            platform,
            displayName,
            "Not set up",
            StatusTone.Neutral,
            "Launcher not added",
            "Verify once to add",
            StatusTone.Neutral,
            IsActive: false,
            IsReady: false,
            PrimaryActionText: "How to Add...",
            PrimaryVariant: ActionButtonVariant.Secondary,
            PrimaryEnabled: isValidDir,
            ShowOverflowMenu: isValidDir,
            CanCreateShortcut: false,
            CanRefreshFiles: false,
            CanOpenFolder: isValidDir,
            StatusTooltip: $"No saved files found for {displayName} yet.",
            PrimaryTooltip: $"See how to add {displayName} to this folder");
    }

    private void UpdateHealthTabAppearance()
    {
        var issueCount = _lastDoctorErrors + _lastDoctorWarnings;
        _tabHealth.Text = issueCount > 0 ? $"Health ({issueCount})" : "Health";
    }

    private void ApplyTheme()
    {
        BackColor = _palette.WindowBg;
        ForeColor = _palette.TextPrimary;

        _headerPanel.BackColor = _palette.SurfaceBg;
        _contentHost.BackColor = _palette.WindowBg;
        _statusPanel.BackColor = _palette.SurfaceBg;
        _lblStatus.ForeColor = _palette.TextSecondary;
        _lblMode.ForeColor = _palette.TextSecondary;

        _segEdition.ApplyTheme(_palette);
        _tabLaunch.ApplyTheme(_palette);
        _tabHealth.ApplyTheme(_palette);
        _tabSettings.ApplyTheme(_palette);

        _launchView.ApplyTheme(_palette);
        _doctorView.ApplyTheme(_palette);
        _settingsView.ApplyTheme(_palette);

        _headerPanel.Invalidate();
        _statusPanel.Invalidate();
    }

    private void UpdateSubViewBusyState(bool subViewBusy)
    {
        var locked = _isBusy || subViewBusy;
        Cursor = locked ? Cursors.WaitCursor : Cursors.Default;
        _segEdition.Enabled = !locked;
        _tabLaunch.Enabled = !locked;
        _tabHealth.Enabled = !locked;
        _tabSettings.Enabled = !locked;
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        var locked = busy || _doctorView.IsBusy;
        Cursor = locked ? Cursors.WaitCursor : Cursors.Default;

        _segEdition.Enabled = !locked;
        _tabLaunch.Enabled = !locked;
        _tabHealth.Enabled = !locked;
        _tabSettings.Enabled = !locked;

        _launchView.SetBusy(busy);
        _doctorView.SetExternalBusy(busy);
        _settingsView.SetBusy(busy);
    }

    private async Task RunUiActionAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _logger.Error("UI action failed.", ex);
            if (!IsDisposed)
            {
                SetStatus($"Error: {ex.Message}");
            }
        }
    }

    private void SetStatus(string text)
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(() =>
            {
                if (!IsDisposed)
                {
                    _lblStatus.Text = text;
                    _toolTip.SetToolTip(_lblStatus, text);
                }
            });
            return;
        }

        _lblStatus.Text = text;
        _toolTip.SetToolTip(_lblStatus, text);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshCts?.Cancel();
            _refreshCts?.Dispose();
            _refreshCts = null;
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    // Internal test seams
    internal AppView CurrentViewForTest => _currentView;
    internal LaunchView LaunchViewForTest => _launchView;
    internal DoctorView DoctorViewForTest => _doctorView;
    internal SettingsView SettingsViewForTest => _settingsView;
    internal Control HeaderPanelForTest => _headerPanel;
    internal Control StatusPanelForTest => _statusPanel;
    internal Label StatusLabelForTest => _lblStatus;
    internal SegmentedControl EditionSegmentForTest => _segEdition;
    internal ActionButton LaunchTabForTest => _tabLaunch;
    internal ActionButton HealthTabForTest => _tabHealth;
    internal ActionButton SettingsTabForTest => _tabSettings;

    internal void SetStatusForTest(string text) => SetStatus(text);
    internal PlatformCardControl GetCardForTest(GamePlatform platform) => _launchView.GetCardForTest(platform);
    internal async Task<DoctorReport?> RefreshStateForTestAsync()
    {
        if (_shownTask is not null)
        {
            await _shownTask;
        }

        return await RefreshStateAsync();
    }
    internal void SetBusyForTest(bool busy) => SetBusy(busy);
    internal Task ToggleThemeForTestAsync() => SetThemeAsync(!_settings.UseDarkTheme);
    internal void NavigateToLaunchForTest() => ShowView(AppView.Launch);
    internal Task NavigateToHealthForTestAsync() => OpenHealthViewAsync();
    internal void NavigateToSettingsForTest() => OpenSettingsView();

    internal void UpdateCardForTest(
        GamePlatform platform,
        string version,
        bool hasProfile,
        bool isOutdated,
        GamePlatform activePlatform,
        bool isInvalidProfile = false,
        EpicManifestInspection? epicInspection = null,
        bool hasActiveEpicSdk = false)
    {
        var model = BuildCardModel(
            platform,
            EditionSpec.GetPlatformDisplayName(platform) switch
            {
                "Rockstar Games Launcher" => "Rockstar Games",
                "Epic Games Launcher" => "Epic Games",
                var x => x
            },
            version,
            hasProfile,
            isOutdated,
            isInvalidProfile,
            activePlatform,
            isValidDir: true,
            epicInspection,
            hasActiveEpicSdk);
        _launchView.UpdateCard(model);
    }

    internal IReadOnlyList<Control> GetBusyGuardedControlsForTest()
    {
        var controls = new List<Control>
        {
            _segEdition.LeftButton,
            _segEdition.RightButton,
            _tabLaunch,
            _tabHealth,
            _tabSettings
        };
        controls.AddRange(_launchView.GetBusyGuardedControlsForTest());
        return controls;
    }
}
