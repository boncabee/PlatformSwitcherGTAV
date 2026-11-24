using System.Drawing;
using System.Windows.Forms;
using Grand_Theft_Auto_V_Platform_Switcher.Models;
using Grand_Theft_Auto_V_Platform_Switcher.UI.Controls;

namespace Grand_Theft_Auto_V_Platform_Switcher.UI.Views;

public sealed class LaunchView : UserControl
{
    private readonly BufferedPanel _scrollHost;
    private readonly BufferedTableLayoutPanel _root;

    // Folder bar
    private readonly CardPanel _folderBar;
    private readonly BufferedTableLayoutPanel _folderLayout;
    private readonly Label _lblFolderTitle;
    private readonly SingleLineEllipsisLabel _lblFolderPath;
    private readonly StatusPill _pillGameVersion;
    private readonly ActionButton _btnDetect;
    private readonly ActionButton _btnBrowse;

    // Cards deck
    private readonly BufferedTableLayoutPanel _cardsDeck;
    private readonly Dictionary<GamePlatform, PlatformCardControl> _cards = new();

    // Launch options bar
    private readonly CardPanel _optionsBar;
    private readonly BufferedTableLayoutPanel _optionsLayout;
    private readonly CheckBox _chkDisableBattlEye;
    private readonly StatusPill _pillBattlEye;
    private readonly ActionButton _btnHealthLink;

    private readonly ToolTip _toolTip;
    private ThemePalette _palette = ThemeManager.Dark;
    private bool _isBusy;
    private bool _hasValidFolder;
    private bool _isUpdatingLayout;

    public event EventHandler? AutoDetectRequested;
    public event EventHandler? BrowseFolderRequested;
    public event EventHandler<bool>? DisableBattlEyeChanged;
    public event EventHandler? OpenHealthRequested;
    public event EventHandler<GamePlatform>? CardPrimaryActionClicked;
    public event EventHandler<GamePlatform>? CardShortcutRequested;
    public event EventHandler<GamePlatform>? CardRefreshFilesRequested;
    public event EventHandler<GamePlatform>? CardOpenFolderRequested;

    public LaunchView(ToolTip toolTip)
    {
        _toolTip = toolTip;

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
            Padding = new Padding(10, 6, 10, 6),
            Margin = new Padding(0)
        };
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // 1. Compact 1-line Folder Bar
        _folderBar = new CardPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(9, 4, 9, 4),
            Margin = new Padding(0, 0, 0, 6),
            CornerRadius = 8
        };

        _folderLayout = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 5,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        _folderLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _folderLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _folderLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _folderLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _folderLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _folderLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _lblFolderTitle = new Label
        {
            Text = "Folder",
            Font = new Font("Segoe UI Semibold", 8.8F),
            UseMnemonic = false,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 8, 0),
            TabStop = false
        };

        _lblFolderPath = new SingleLineEllipsisLabel
        {
            Text = "No game folder selected — click Detect or Change",
            Font = new Font("Segoe UI", 8.6F),
            UsePathEllipsis = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 8, 0)
        };

        _pillGameVersion = new StatusPill
        {
            Font = new Font("Segoe UI Semibold", 8F),
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 0, 8, 0),
            Visible = false
        };

        _btnDetect = new ActionButton
        {
            Text = "Detect",
            Font = new Font("Segoe UI", 8.4F),
            AutoSize = true,
            Padding = new Padding(6, 2, 6, 2),
            Margin = new Padding(0, 0, 4, 0),
            Anchor = AnchorStyles.Right,
            AccessibleName = "Auto-detect game folder"
        };
        _btnBrowse = new ActionButton
        {
            Text = "Change...",
            Font = new Font("Segoe UI", 8.4F),
            AutoSize = true,
            Padding = new Padding(6, 2, 6, 2),
            Margin = new Padding(0),
            Anchor = AnchorStyles.Right,
            AccessibleName = "Browse game folder"
        };

        _btnDetect.Click += (_, _) => AutoDetectRequested?.Invoke(this, EventArgs.Empty);
        _btnBrowse.Click += (_, _) => BrowseFolderRequested?.Invoke(this, EventArgs.Empty);
        _toolTip.SetToolTip(_btnDetect, "Automatically locate your GTA V installation folder");
        _toolTip.SetToolTip(_btnBrowse, "Select your GTA V folder manually");

        _folderLayout.Controls.Add(_lblFolderTitle, 0, 0);
        _folderLayout.Controls.Add(_lblFolderPath, 1, 0);
        _folderLayout.Controls.Add(_pillGameVersion, 2, 0);
        _folderLayout.Controls.Add(_btnDetect, 3, 0);
        _folderLayout.Controls.Add(_btnBrowse, 4, 0);
        _folderBar.Controls.Add(_folderLayout);

        // 2. 3-Column Platform Cards Deck
        _cardsDeck = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 6),
            Padding = new Padding(0)
        };
        _cardsDeck.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3333F));
        _cardsDeck.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3333F));
        _cardsDeck.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3334F));
        _cardsDeck.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        AddPlatformCard(GamePlatform.RGL, "Rockstar Games", "RGL.png", 0, new Padding(0, 0, 4, 0));
        AddPlatformCard(GamePlatform.Steam, "Steam", "Steam.png", 1, new Padding(4, 0, 4, 0));
        AddPlatformCard(GamePlatform.EGL, "Epic Games", "EGL.png", 2, new Padding(4, 0, 0, 0));

        // 3. Compact 1-line Launch Options Bar
        _optionsBar = new CardPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(9, 4, 9, 4),
            Margin = new Padding(0),
            CornerRadius = 8
        };

        _optionsLayout = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        _optionsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _optionsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _optionsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _optionsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _chkDisableBattlEye = new CheckBox
        {
            Text = "Story Mode / Mods (disable BattlEye)",
            Font = new Font("Segoe UI", 8.7F),
            UseMnemonic = false,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 1, 8, 1),
            AccessibleName = "Disable BattlEye for Story Mode and Mods"
        };
        _chkDisableBattlEye.CheckedChanged += (_, _) =>
            DisableBattlEyeChanged?.Invoke(this, _chkDisableBattlEye.Checked);
        _toolTip.SetToolTip(
            _chkDisableBattlEye,
            "Turn this off before playing GTA Online (GTA Online requires BattlEye).");

        _pillBattlEye = new StatusPill
        {
            Font = new Font("Segoe UI Semibold", 8F),
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 0, 6, 0)
        };

        _btnHealthLink = new ActionButton
        {
            Text = "1 issue →",
            Font = new Font("Segoe UI Semibold", 8.3F),
            Variant = ActionButtonVariant.SubtleLink,
            AutoSize = true,
            Padding = new Padding(4, 2, 4, 2),
            Margin = new Padding(0),
            Anchor = AnchorStyles.Right,
            Visible = false,
            AccessibleName = "View health issues"
        };
        _btnHealthLink.Click += (_, _) => OpenHealthRequested?.Invoke(this, EventArgs.Empty);

        _optionsLayout.Controls.Add(_chkDisableBattlEye, 0, 0);
        _optionsLayout.Controls.Add(_pillBattlEye, 1, 0);
        _optionsLayout.Controls.Add(_btnHealthLink, 2, 0);
        _optionsBar.Controls.Add(_optionsLayout);

        _root.Controls.Add(_folderBar, 0, 0);
        _root.Controls.Add(_cardsDeck, 0, 1);
        _root.Controls.Add(_optionsBar, 0, 2);

        _scrollHost.Controls.Add(_root);
        Controls.Add(_scrollHost);

        _scrollHost.SizeChanged += (_, _) => UpdateLayout();
        SizeChanged += (_, _) => UpdateLayout();
    }

    private void AddPlatformCard(GamePlatform platform, string title, string iconAsset, int column, Padding margin)
    {
        var card = new PlatformCardControl(platform, title, iconAsset, _toolTip)
        {
            Margin = margin
        };
        card.PrimaryActionClicked += (s, p) => CardPrimaryActionClicked?.Invoke(s, p);
        card.CreateShortcutRequested += (s, p) => CardShortcutRequested?.Invoke(s, p);
        card.RefreshFilesRequested += (s, p) => CardRefreshFilesRequested?.Invoke(s, p);
        card.OpenFolderRequested += (s, p) => CardOpenFolderRequested?.Invoke(s, p);
        _cards[platform] = card;
        _cardsDeck.Controls.Add(card, column, 0);
    }

    public void SetDisableBattlEyeChecked(bool isChecked)
    {
        if (_chkDisableBattlEye.Checked != isChecked)
        {
            _chkDisableBattlEye.Checked = isChecked;
        }
    }

    public void BindState(
        string gameDirectory,
        bool isValidDirectory,
        string activeVersion,
        PlatformCardModel rglModel,
        PlatformCardModel steamModel,
        PlatformCardModel eglModel,
        string battlEyePillText,
        StatusTone battlEyeTone,
        string battlEyeTooltip,
        int healthErrorCount,
        int healthWarningCount)
    {
        _hasValidFolder = isValidDirectory;

        if (!string.IsNullOrWhiteSpace(gameDirectory) && isValidDirectory)
        {
            _lblFolderPath.Text = gameDirectory;
            _lblFolderPath.ForeColor = _palette.TextPrimary;
            _toolTip.SetToolTip(_lblFolderPath, gameDirectory);
        }
        else if (!string.IsNullOrWhiteSpace(gameDirectory))
        {
            _lblFolderPath.Text = $"{gameDirectory} (game executable not found)";
            _lblFolderPath.ForeColor = _palette.WarningText;
            _toolTip.SetToolTip(_lblFolderPath, gameDirectory);
        }
        else
        {
            _lblFolderPath.Text = "No game folder selected — click Detect or Change";
            _lblFolderPath.ForeColor = _palette.WarningText;
            _toolTip.SetToolTip(_lblFolderPath, null);
        }

        var showVer = isValidDirectory &&
                      !string.IsNullOrWhiteSpace(activeVersion) &&
                      !activeVersion.Equals("Unknown", StringComparison.OrdinalIgnoreCase);
        _pillGameVersion.Visible = showVer;
        if (showVer)
        {
            var verLabel = char.IsDigit(activeVersion[0]) ? $"v{activeVersion}" : "Installed";
            _pillGameVersion.SetState(verLabel, StatusTone.Neutral);
        }

        _cards[GamePlatform.RGL].Bind(rglModel);
        _cards[GamePlatform.Steam].Bind(steamModel);
        _cards[GamePlatform.EGL].Bind(eglModel);

        _pillBattlEye.SetState(battlEyePillText, battlEyeTone);
        _toolTip.SetToolTip(_pillBattlEye, battlEyeTooltip);

        var totalIssues = healthErrorCount + healthWarningCount;
        _btnHealthLink.Visible = totalIssues > 0;
        if (totalIssues > 0)
        {
            _btnHealthLink.Text = totalIssues == 1 ? "1 issue →" : $"{totalIssues} issues →";
            _toolTip.SetToolTip(_btnHealthLink, "Open the Health tab to inspect or fix issues");
        }

        UpdateLayout();
    }

    public void UpdateCard(PlatformCardModel model)
    {
        if (_cards.TryGetValue(model.Platform, out var card))
        {
            card.Bind(model);
        }
    }

    public void ApplyTheme(ThemePalette palette)
    {
        _palette = palette;
        BackColor = palette.WindowBg;
        ForeColor = palette.TextPrimary;
        _scrollHost.BackColor = palette.WindowBg;
        _root.BackColor = palette.WindowBg;
        _cardsDeck.BackColor = palette.WindowBg;

        _folderBar.ApplyTheme(palette);
        _lblFolderTitle.ForeColor = palette.TextPrimary;
        _lblFolderPath.ForeColor = _hasValidFolder ? palette.TextPrimary : palette.WarningText;
        _pillGameVersion.ApplyTheme(palette);
        _btnDetect.ApplyTheme(palette);
        _btnBrowse.ApplyTheme(palette);

        foreach (var card in _cards.Values)
        {
            card.ApplyTheme(palette);
        }

        _optionsBar.ApplyTheme(palette);
        _chkDisableBattlEye.ForeColor = palette.TextPrimary;
        _pillBattlEye.ApplyTheme(palette);
        _btnHealthLink.ApplyTheme(palette);
    }

    public void SetBusy(bool busy)
    {
        _isBusy = busy;
        _btnDetect.Enabled = !busy;
        _btnBrowse.Enabled = !busy;
        _chkDisableBattlEye.Enabled = !busy;
        _btnHealthLink.Enabled = !busy;
        foreach (var card in _cards.Values)
        {
            card.SetBusy(busy);
        }
    }

    public void UpdateLayout()
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
            var usableW = Math.Max(260, hostW - _root.Padding.Horizontal);

            var reservedOptW = _pillBattlEye.PreferredSize.Width + _pillBattlEye.Margin.Horizontal
                + (_btnHealthLink.Visible ? _btnHealthLink.PreferredSize.Width + _btnHealthLink.Margin.Horizontal : 0)
                + _optionsBar.Padding.Horizontal + 20;
            _chkDisableBattlEye.MaximumSize = new Size(Math.Max(140, usableW - reservedOptW), 0);

            var fallbackCardW = Math.Max(140, (usableW - 16) / 3);
            foreach (var card in _cards.Values)
            {
                card.UpdateTextWrappingBounds(fallbackCardW);
            }

            var folderPref = _folderBar.GetPreferredSize(new Size(usableW, 0));
            var optionsPref = _optionsBar.GetPreferredSize(new Size(usableW, 0));
            var minCardHeight = _cards.Values.Select(c => c.GetMinimumContentHeight()).DefaultIfEmpty(180).Max();
            _cardsDeck.MinimumSize = new Size(0, minCardHeight);

            var requiredHeight = _root.Padding.Vertical
                + folderPref.Height + _folderBar.Margin.Vertical
                + minCardHeight + _cardsDeck.Margin.Vertical
                + optionsPref.Height + _optionsBar.Margin.Vertical;

            var needsScroll = requiredHeight > hostH && hostH > 0;

            _root.SuspendLayout();
            if (needsScroll)
            {
                _scrollHost.AutoScroll = true;
                var scrollW = Math.Max(260, _scrollHost.ClientSize.Width);
                _root.Dock = DockStyle.Top;
                _root.MinimumSize = new Size(scrollW, requiredHeight);
                _root.MaximumSize = new Size(scrollW, 0);
            }
            else
            {
                _scrollHost.AutoScroll = false;
                _scrollHost.AutoScrollMinSize = Size.Empty;
                _root.MinimumSize = Size.Empty;
                _root.MaximumSize = Size.Empty;
                _root.Dock = DockStyle.Fill;
            }
            _root.ResumeLayout(true);

            foreach (var card in _cards.Values)
            {
                card.UpdateTextWrappingBounds(fallbackCardW);
            }
        }
        finally
        {
            _isUpdatingLayout = false;
        }
    }

    internal BufferedPanel ScrollHostForTest => _scrollHost;
    internal ActionButton DetectButtonForTest => _btnDetect;
    internal ActionButton BrowseButtonForTest => _btnBrowse;
    internal CheckBox DisableBattlEyeCheckBoxForTest => _chkDisableBattlEye;
    internal StatusPill BattlEyePillForTest => _pillBattlEye;
    internal ActionButton HealthLinkButtonForTest => _btnHealthLink;
    internal PlatformCardControl GetCardForTest(GamePlatform platform) => _cards[platform];

    internal IReadOnlyList<Control> GetBusyGuardedControlsForTest()
    {
        var list = new List<Control>
        {
            _btnDetect,
            _btnBrowse,
            _chkDisableBattlEye,
            _btnHealthLink
        };
        foreach (var card in _cards.Values)
        {
            list.Add(card.PrimaryButtonForTest);
            list.Add(card.OverflowButtonForTest);
        }
        return list;
    }
}
