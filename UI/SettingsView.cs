using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Grand_Theft_Auto_V_Platform_Switcher.Models;
using Grand_Theft_Auto_V_Platform_Switcher.Services;
using Grand_Theft_Auto_V_Platform_Switcher.UI.Controls;

namespace Grand_Theft_Auto_V_Platform_Switcher.UI;

public sealed class SettingsView : UserControl
{
    private const string RepoUrl = "https://github.com/boncabee/PlatformSwitcherGTAV";
    private const string ReleasesUrl = "https://github.com/boncabee/PlatformSwitcherGTAV/releases";

    private readonly AppLogger _logger;
    private ThemePalette _palette = ThemeManager.Dark;
    private bool _isBusy;
    private bool _isCheckingUpdates;
    private bool _isLoading;
    private bool _isUpdatingLayout;

    private readonly BufferedPanel _scrollHost;
    private readonly BufferedTableLayoutPanel _root;

    // 1. Game Folders Card
    private readonly CardPanel _foldersCard;
    private readonly Label _lblFoldersHeader;
    private readonly Label _lblLegacyLabel;
    private readonly TextBox _txtLegacyPath;
    private readonly ActionButton _btnDetectLegacy;
    private readonly ActionButton _btnBrowseLegacy;
    private readonly Label _lblEnhancedLabel;
    private readonly TextBox _txtEnhancedPath;
    private readonly ActionButton _btnDetectEnhanced;
    private readonly ActionButton _btnBrowseEnhanced;

    // 2. Middle 2-Column Row
    private readonly BufferedTableLayoutPanel _middleRow;
    private readonly CardPanel _appearanceCard;
    private readonly Label _lblAppearanceHeader;
    private readonly Label _lblThemeLabel;
    private readonly SegmentedControl _segTheme;
    private readonly CheckBox _chkCheckUpdates;
    private readonly ActionButton _btnCheckUpdates;
    private readonly Label _lblUpdateStatus;

    private readonly CardPanel _advancedCard;
    private readonly Label _lblAdvancedHeader;
    private readonly Label _lblEpicIdLabel;
    private readonly TextBox _txtEpicEnhancedId;
    private readonly ActionButton _btnLogs;
    private readonly ActionButton _btnSaveEpicId;
    private readonly Label _lblEpicIdFeedback;

    // 3. Compact About Card
    private readonly CardPanel _aboutCard;
    private readonly PictureBox _logo;
    private readonly Label _lblAboutTitle;
    private readonly Label _lblAboutSubtitle;
    private readonly LinkLabel _linkRepo;
    private readonly LinkLabel _linkReleases;

    public event EventHandler<bool>? ThemeSelected;
    public event EventHandler<bool>? CheckUpdatesOnStartupChanged;
    public event EventHandler<GameEdition>? DetectFolderRequested;
    public event EventHandler<(GameEdition Edition, string Directory)>? GameFolderChanged;
    public event EventHandler<string>? EpicAppIdOverrideSaved;
    public event EventHandler? CheckUpdatesNowRequested;

    public SettingsView(AppLogger logger)
    {
        _logger = logger;
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2.0.0";

        AutoScaleMode = AutoScaleMode.Inherit;
        Dock = DockStyle.Fill;
        Margin = new Padding(0);
        Padding = new Padding(0);

        _scrollHost = new BufferedPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        _root = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10, 4, 10, 4),
            Margin = new Padding(0)
        };
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // Section 1: Game Folders
        _foldersCard = new CardPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(9, 4, 9, 4),
            Margin = new Padding(0, 0, 0, 4),
            CornerRadius = 8
        };

        var foldersLayout = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 4,
            RowCount = 3,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        foldersLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        foldersLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        foldersLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        foldersLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        foldersLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        foldersLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        foldersLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _lblFoldersHeader = new Label
        {
            Text = "Game Folders",
            Font = new Font("Segoe UI Semibold", 9.2F),
            UseMnemonic = false,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2),
            TabStop = false
        };
        foldersLayout.SetColumnSpan(_lblFoldersHeader, 4);

        _lblLegacyLabel = new Label
        {
            Text = "Legacy",
            Font = new Font("Segoe UI Semibold", 8.6F),
            UseMnemonic = false,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 10, 3),
            TabStop = false
        };
        _txtLegacyPath = CreateReadOnlyPathBox();
        _btnDetectLegacy = CreateCompactActionButton("Detect", new Padding(0, 0, 4, 3));
        _btnBrowseLegacy = CreateCompactActionButton("Browse...", new Padding(0, 0, 0, 3));

        _lblEnhancedLabel = new Label
        {
            Text = "Enhanced",
            Font = new Font("Segoe UI Semibold", 8.6F),
            UseMnemonic = false,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 10, 0),
            TabStop = false
        };
        _txtEnhancedPath = CreateReadOnlyPathBox();
        _txtEnhancedPath.Margin = new Padding(0, 1, 6, 0);
        _btnDetectEnhanced = CreateCompactActionButton("Detect", new Padding(0, 0, 4, 0));
        _btnBrowseEnhanced = CreateCompactActionButton("Browse...", new Padding(0));

        _btnDetectLegacy.Click += (_, _) => DetectFolderRequested?.Invoke(this, GameEdition.Legacy);
        _btnDetectEnhanced.Click += (_, _) => DetectFolderRequested?.Invoke(this, GameEdition.Enhanced);
        _btnBrowseLegacy.Click += (_, _) => BrowseFolder(GameEdition.Legacy, _txtLegacyPath, "Select GTA V Legacy folder");
        _btnBrowseEnhanced.Click += (_, _) => BrowseFolder(GameEdition.Enhanced, _txtEnhancedPath, "Select GTA V Enhanced folder");

        foldersLayout.Controls.Add(_lblFoldersHeader, 0, 0);
        foldersLayout.Controls.Add(_lblLegacyLabel, 0, 1);
        foldersLayout.Controls.Add(_txtLegacyPath, 1, 1);
        foldersLayout.Controls.Add(_btnDetectLegacy, 2, 1);
        foldersLayout.Controls.Add(_btnBrowseLegacy, 3, 1);
        foldersLayout.Controls.Add(_lblEnhancedLabel, 0, 2);
        foldersLayout.Controls.Add(_txtEnhancedPath, 1, 2);
        foldersLayout.Controls.Add(_btnDetectEnhanced, 2, 2);
        foldersLayout.Controls.Add(_btnBrowseEnhanced, 3, 2);
        _foldersCard.Controls.Add(foldersLayout);

        // Section 2: Middle 2-Column Split
        _middleRow = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 4),
            Padding = new Padding(0)
        };
        _middleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _middleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _middleRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // 2A: Appearance & Updates
        _appearanceCard = new CardPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(9, 4, 9, 4),
            Margin = new Padding(0, 0, 4, 0),
            CornerRadius = 8
        };
        var appearanceLayout = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 4,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        appearanceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        appearanceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        appearanceLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        appearanceLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        appearanceLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        appearanceLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _lblAppearanceHeader = new Label
        {
            Text = "Appearance & Updates",
            Font = new Font("Segoe UI Semibold", 9.2F),
            UseMnemonic = false,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2),
            TabStop = false
        };
        appearanceLayout.SetColumnSpan(_lblAppearanceHeader, 2);

        _lblThemeLabel = new Label
        {
            Text = "Theme",
            Font = new Font("Segoe UI", 8.7F),
            UseMnemonic = false,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 10, 3),
            TabStop = false
        };

        _segTheme = new SegmentedControl("Dark", "Light")
        {
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 0, 3)
        };
        _segTheme.SelectedIndexChanged += (_, idx) =>
        {
            if (!_isLoading)
            {
                ThemeSelected?.Invoke(this, idx == 0);
            }
        };

        _chkCheckUpdates = new CheckBox
        {
            Text = "Check for updates on startup",
            Font = new Font("Segoe UI", 8.6F),
            UseMnemonic = false,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 3)
        };
        _chkCheckUpdates.CheckedChanged += (_, _) =>
        {
            if (!_isLoading)
            {
                CheckUpdatesOnStartupChanged?.Invoke(this, _chkCheckUpdates.Checked);
            }
        };
        appearanceLayout.SetColumnSpan(_chkCheckUpdates, 2);

        _btnCheckUpdates = CreateCompactActionButton("Check Now", new Padding(0, 0, 8, 0));
        _btnCheckUpdates.Anchor = AnchorStyles.Left;
        _btnCheckUpdates.Click += (_, _) => CheckUpdatesNowRequested?.Invoke(this, EventArgs.Empty);

        _lblUpdateStatus = new Label
        {
            Text = $"Installed v{version}",
            Font = new Font("Segoe UI", 8.2F),
            UseMnemonic = false,
            AutoSize = false,
            AutoEllipsis = true,
            Size = new Size(10, 1),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0),
            TabStop = false
        };

        appearanceLayout.Controls.Add(_lblAppearanceHeader, 0, 0);
        appearanceLayout.Controls.Add(_lblThemeLabel, 0, 1);
        appearanceLayout.Controls.Add(_segTheme, 1, 1);
        appearanceLayout.Controls.Add(_chkCheckUpdates, 0, 2);
        appearanceLayout.Controls.Add(_btnCheckUpdates, 0, 3);
        appearanceLayout.Controls.Add(_lblUpdateStatus, 1, 3);
        _appearanceCard.Controls.Add(appearanceLayout);

        // 2B: Advanced
        _advancedCard = new CardPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(9, 4, 9, 4),
            Margin = new Padding(4, 0, 0, 0),
            CornerRadius = 8
        };
        var advancedLayout = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = 4,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        advancedLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        advancedLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        advancedLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        advancedLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        advancedLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        advancedLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        advancedLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _lblAdvancedHeader = new Label
        {
            Text = "Advanced",
            Font = new Font("Segoe UI Semibold", 9.2F),
            UseMnemonic = false,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2),
            TabStop = false
        };
        advancedLayout.SetColumnSpan(_lblAdvancedHeader, 3);

        _lblEpicIdLabel = new Label
        {
            Text = "Enhanced Epic App ID (optional)",
            Font = new Font("Segoe UI", 8.5F),
            UseMnemonic = false,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 1),
            TabStop = false
        };
        advancedLayout.SetColumnSpan(_lblEpicIdLabel, 3);

        _txtEpicEnhancedId = new TextBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "Leave blank for auto-detect",
            Margin = new Padding(0, 0, 0, 4)
        };
        advancedLayout.SetColumnSpan(_txtEpicEnhancedId, 3);

        _btnLogs = CreateCompactActionButton("Open Logs", new Padding(0, 0, 6, 0));
        _btnLogs.Anchor = AnchorStyles.Left;
        _btnLogs.Click += (_, _) => OpenLogsFolder();

        _lblEpicIdFeedback = new Label
        {
            Text = string.Empty,
            Font = new Font("Segoe UI", 8.2F),
            UseMnemonic = false,
            AutoSize = false,
            AutoEllipsis = true,
            Size = new Size(10, 1),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0),
            TabStop = false
        };

        _btnSaveEpicId = new ActionButton
        {
            Text = "Save ID",
            Font = new Font("Segoe UI Semibold", 8.3F),
            Variant = ActionButtonVariant.PrimaryAccent,
            AutoSize = true,
            Padding = new Padding(7, 0, 7, 0),
            Margin = new Padding(0, 0, 6, 0),
            Anchor = AnchorStyles.Left
        };
        _btnSaveEpicId.Click += (_, _) =>
        {
            EpicAppIdOverrideSaved?.Invoke(this, _txtEpicEnhancedId.Text.Trim());
            _lblEpicIdFeedback.Text = "Saved";
            _lblEpicIdFeedback.ForeColor = _palette.SuccessText;
        };

        advancedLayout.Controls.Add(_lblAdvancedHeader, 0, 0);
        advancedLayout.Controls.Add(_lblEpicIdLabel, 0, 1);
        advancedLayout.Controls.Add(_txtEpicEnhancedId, 0, 2);
        advancedLayout.Controls.Add(_btnLogs, 0, 3);
        advancedLayout.Controls.Add(_btnSaveEpicId, 1, 3);
        advancedLayout.Controls.Add(_lblEpicIdFeedback, 2, 3);
        _advancedCard.Controls.Add(advancedLayout);

        _middleRow.Controls.Add(_appearanceCard, 0, 0);
        _middleRow.Controls.Add(_advancedCard, 1, 0);

        // Section 3: Compact About Strip
        _aboutCard = new CardPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(9, 4, 9, 4),
            Margin = new Padding(0),
            CornerRadius = 8
        };
        var aboutLayout = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 4,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        aboutLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        aboutLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        aboutLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        aboutLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        aboutLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _logo = new PictureBox
        {
            Image = AppAssets.LoadImage("Switcher.png"),
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(24, 24),
            Margin = new Padding(0, 0, 10, 0),
            Anchor = AnchorStyles.Left,
            TabStop = false
        };

        var aboutTextStack = new BufferedTableLayoutPanel
        {
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.Transparent,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 0, 8, 0),
            Padding = new Padding(0)
        };
        aboutTextStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        aboutTextStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        aboutTextStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _lblAboutTitle = new Label
        {
            Text = $"Grand Theft Auto V Platform Switcher v{version} · by boncabee & TechnoStone",
            Font = new Font("Segoe UI Semibold", 8.6F),
            UseMnemonic = false,
            AutoSize = false,
            AutoEllipsis = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0),
            TabStop = false
        };
        _lblAboutTitle.Height = _lblAboutTitle.Font.Height + 1;
        _lblAboutTitle.FontChanged += (_, _) => _lblAboutTitle.Height = _lblAboutTitle.Font.Height + 1;

        _lblAboutSubtitle = new Label
        {
            Text = "Play GTA V (Legacy or Enhanced) across Rockstar Games, Steam, and Epic Games using one folder.",
            Font = new Font("Segoe UI", 8F),
            UseMnemonic = false,
            AutoSize = false,
            AutoEllipsis = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0),
            TabStop = false
        };
        _lblAboutSubtitle.Height = _lblAboutSubtitle.Font.Height + 1;
        _lblAboutSubtitle.FontChanged += (_, _) => _lblAboutSubtitle.Height = _lblAboutSubtitle.Font.Height + 1;

        aboutTextStack.Controls.Add(_lblAboutTitle, 0, 0);
        aboutTextStack.Controls.Add(_lblAboutSubtitle, 0, 1);

        _linkRepo = new LinkLabel
        {
            Text = "GitHub",
            Font = new Font("Segoe UI", 8.5F),
            UseMnemonic = false,
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(4, 2, 10, 2)
        };
        _linkRepo.LinkClicked += (_, _) => OpenUrl(RepoUrl);

        _linkReleases = new LinkLabel
        {
            Text = "Releases",
            Font = new Font("Segoe UI", 8.5F),
            UseMnemonic = false,
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 2, 0, 2)
        };
        _linkReleases.LinkClicked += (_, _) => OpenUrl(ReleasesUrl);

        aboutLayout.Controls.Add(_logo, 0, 0);
        aboutLayout.Controls.Add(aboutTextStack, 1, 0);
        aboutLayout.Controls.Add(_linkRepo, 2, 0);
        aboutLayout.Controls.Add(_linkReleases, 3, 0);
        _aboutCard.Controls.Add(aboutLayout);

        _root.Controls.Add(_foldersCard, 0, 0);
        _root.Controls.Add(_middleRow, 0, 1);
        _root.Controls.Add(_aboutCard, 0, 2);

        _scrollHost.Controls.Add(_root);
        Controls.Add(_scrollHost);

        _scrollHost.SizeChanged += (_, _) => UpdateLayout();
        SizeChanged += (_, _) => UpdateLayout();
    }

    private static TextBox CreateReadOnlyPathBox() => new()
    {
        ReadOnly = true,
        Dock = DockStyle.Fill,
        BorderStyle = BorderStyle.FixedSingle,
        PlaceholderText = "Not configured",
        Margin = new Padding(0, 0, 6, 3)
    };

    private static ActionButton CreateCompactActionButton(string text, Padding margin) => new()
    {
        Text = text,
        Font = new Font("Segoe UI", 8.3F),
        AutoSize = true,
        Padding = new Padding(6, 0, 6, 0),
        Margin = margin,
        Anchor = AnchorStyles.Right
    };

    private void BrowseFolder(GameEdition edition, TextBox targetBox, string description)
    {
        if (_isBusy)
        {
            return;
        }

        using var dialog = new FolderBrowserDialog
        {
            Description = description,
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(targetBox.Text) ? targetBox.Text : string.Empty
        };

        if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
        {
            targetBox.Text = dialog.SelectedPath;
            GameFolderChanged?.Invoke(this, (edition, dialog.SelectedPath));
        }
    }

    public void LoadFromSettings(AppSettings settings)
    {
        _isLoading = true;
        try
        {
            _txtLegacyPath.Text = settings.Legacy.GameDirectory;
            _txtEnhancedPath.Text = settings.Enhanced.GameDirectory;
            _txtEpicEnhancedId.Text = settings.Enhanced.EpicAppIdOverride;
            _segTheme.SelectedIndex = settings.UseDarkTheme ? 0 : 1;
            _chkCheckUpdates.Checked = settings.CheckUpdatesOnStartup;
            _lblEpicIdFeedback.Text = string.Empty;
        }
        finally
        {
            _isLoading = false;
        }

        UpdateLayout();
    }

    public void SetUpdateStatus(string text, StatusTone tone = StatusTone.Neutral, bool checking = false)
    {
        _isCheckingUpdates = checking;
        _lblUpdateStatus.Text = text;
        _lblUpdateStatus.ForeColor = tone switch
        {
            StatusTone.Success => _palette.SuccessText,
            StatusTone.Warning => _palette.WarningText,
            StatusTone.Danger => _palette.DangerText,
            StatusTone.Accent => _palette.AccentText,
            _ => _palette.TextSecondary
        };
        _btnCheckUpdates.Enabled = !_isBusy && !_isCheckingUpdates;
    }

    public void ApplyTheme(ThemePalette palette)
    {
        _palette = palette;
        BackColor = palette.WindowBg;
        ForeColor = palette.TextPrimary;
        _scrollHost.BackColor = palette.WindowBg;
        _root.BackColor = palette.WindowBg;
        _middleRow.BackColor = palette.WindowBg;

        _foldersCard.ApplyTheme(palette);
        _appearanceCard.ApplyTheme(palette);
        _advancedCard.ApplyTheme(palette);
        _aboutCard.ApplyTheme(palette);

        _lblFoldersHeader.ForeColor = palette.TextPrimary;
        _lblLegacyLabel.ForeColor = palette.TextPrimary;
        _lblEnhancedLabel.ForeColor = palette.TextPrimary;
        _lblAppearanceHeader.ForeColor = palette.TextPrimary;
        _lblThemeLabel.ForeColor = palette.TextPrimary;
        _chkCheckUpdates.ForeColor = palette.TextPrimary;
        _lblAdvancedHeader.ForeColor = palette.TextPrimary;
        _lblEpicIdLabel.ForeColor = palette.TextPrimary;
        _lblUpdateStatus.ForeColor = palette.TextSecondary;
        _lblEpicIdFeedback.ForeColor = palette.SuccessText;
        _lblAboutTitle.ForeColor = palette.TextPrimary;
        _lblAboutSubtitle.ForeColor = palette.TextSecondary;

        _linkRepo.LinkColor = palette.AccentText;
        _linkRepo.ActiveLinkColor = palette.Accent;
        _linkReleases.LinkColor = palette.AccentText;
        _linkReleases.ActiveLinkColor = palette.Accent;

        foreach (var txt in new[] { _txtLegacyPath, _txtEnhancedPath, _txtEpicEnhancedId })
        {
            txt.BackColor = palette.InputBg;
            txt.ForeColor = palette.TextPrimary;
        }

        _segTheme.ApplyTheme(palette);
        _isLoading = true;
        try
        {
            _segTheme.SelectedIndex = palette.IsDark ? 0 : 1;
        }
        finally
        {
            _isLoading = false;
        }

        _btnDetectLegacy.ApplyTheme(palette);
        _btnBrowseLegacy.ApplyTheme(palette);
        _btnDetectEnhanced.ApplyTheme(palette);
        _btnBrowseEnhanced.ApplyTheme(palette);
        _btnCheckUpdates.ApplyTheme(palette);
        _btnLogs.ApplyTheme(palette);
        _btnSaveEpicId.ApplyTheme(palette);
    }

    public void SetBusy(bool busy)
    {
        _isBusy = busy;
        _btnDetectLegacy.Enabled = !busy;
        _btnBrowseLegacy.Enabled = !busy;
        _btnDetectEnhanced.Enabled = !busy;
        _btnBrowseEnhanced.Enabled = !busy;
        _segTheme.Enabled = !busy;
        _chkCheckUpdates.Enabled = !busy;
        _btnCheckUpdates.Enabled = !busy && !_isCheckingUpdates;
        _txtEpicEnhancedId.Enabled = !busy;
        _btnLogs.Enabled = !busy;
        _btnSaveEpicId.Enabled = !busy;
    }

    internal void UpdateLayout()
    {
        if (_isUpdatingLayout || IsDisposed || _scrollHost.IsDisposed || _scrollHost.ClientSize.Width <= 0)
        {
            return;
        }

        _isUpdatingLayout = true;
        try
        {
            var hostW = _scrollHost.ClientSize.Width;
            var hostH = Math.Max(0, _scrollHost.ClientSize.Height);
            var colW = Math.Max(140, (hostW - _root.Padding.Horizontal - 8) / 2 - _appearanceCard.Padding.Horizontal - 8);
            _chkCheckUpdates.MaximumSize = new Size(colW, 0);
            _lblEpicIdLabel.MaximumSize = new Size(colW, 0);

            _root.SuspendLayout();
            _root.Dock = DockStyle.Top;
            _root.MinimumSize = Size.Empty;
            _root.MaximumSize = new Size(hostW, 0);
            _root.ResumeLayout(true);

            var pref = _root.GetPreferredSize(new Size(hostW, 0));
            var needsScroll = pref.Height > hostH && hostH > 0;

            _root.SuspendLayout();
            if (needsScroll)
            {
                _scrollHost.AutoScroll = true;
                var scrollW = Math.Max(260, _scrollHost.ClientSize.Width);
                _root.Dock = DockStyle.Top;
                _root.MinimumSize = new Size(scrollW, 0);
                _root.MaximumSize = new Size(scrollW, 0);
            }
            else
            {
                _scrollHost.AutoScroll = false;
                _scrollHost.AutoScrollMinSize = Size.Empty;
                _root.MinimumSize = Size.Empty;
                _root.MaximumSize = new Size(hostW, 0);
                _root.Dock = DockStyle.Top;
            }
            _root.ResumeLayout(true);
        }
        finally
        {
            _isUpdatingLayout = false;
        }
    }

    private void OpenLogsFolder()
    {
        try
        {
            Directory.CreateDirectory(_logger.LogDirectory);
            Process.Start(new ProcessStartInfo(_logger.LogDirectory) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(FindForm(), $"Unable to open logs folder: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // Ignore browser launch errors.
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _logo.Image?.Dispose();
            _logo.Image = null;
        }
        base.Dispose(disposing);
    }

    internal BufferedPanel ScrollHostForTest => _scrollHost;
    internal SegmentedControl ThemeToggleForTest => _segTheme;
    internal CheckBox CheckUpdatesOnStartupCheckBoxForTest => _chkCheckUpdates;
    internal ActionButton CheckUpdatesNowButtonForTest => _btnCheckUpdates;
    internal ActionButton BrowseLegacyButtonForTest => _btnBrowseLegacy;
    internal ActionButton BrowseEnhancedButtonForTest => _btnBrowseEnhanced;
    internal ActionButton LogsButtonForTest => _btnLogs;
    internal ActionButton SaveEpicIdButtonForTest => _btnSaveEpicId;
    internal TextBox LegacyPathTextBoxForTest => _txtLegacyPath;
    internal TextBox EnhancedPathTextBoxForTest => _txtEnhancedPath;
    internal LinkLabel RepoLinkForTest => _linkRepo;
    internal LinkLabel ReleasesLinkForTest => _linkReleases;
}
