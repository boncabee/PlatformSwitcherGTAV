using System.Drawing;
using System.Windows.Forms;
using Grand_Theft_Auto_V_Platform_Switcher.Models;
using Grand_Theft_Auto_V_Platform_Switcher.UI.Controls;

namespace Grand_Theft_Auto_V_Platform_Switcher.UI.Views;

public sealed record PlatformCardModel(
    GamePlatform Platform,
    string DisplayName,
    string StatusText,
    StatusTone StatusTone,
    string VersionText,
    string HintText,
    StatusTone HintTone,
    bool IsActive,
    bool IsReady,
    string PrimaryActionText,
    ActionButtonVariant PrimaryVariant,
    bool PrimaryEnabled,
    bool ShowOverflowMenu,
    bool CanCreateShortcut,
    bool CanRefreshFiles,
    bool CanOpenFolder,
    string? StatusTooltip = null,
    string? PrimaryTooltip = null);

public sealed class PlatformCardControl : UserControl
{
    private readonly CardPanel _card;
    private readonly BufferedTableLayoutPanel _layout;
    private readonly PictureBox _logo;
    private readonly Label _lblTitle;
    private readonly StatusPill _pillStatus;
    private readonly Label _lblVersion;
    private readonly Label _lblHint;
    private readonly BufferedTableLayoutPanel _actionsRow;
    private readonly ActionButton _btnPrimary;
    private readonly ActionButton _btnOverflow;
    private readonly ContextMenuStrip _overflowMenu;
    private readonly ToolStripMenuItem _miShortcut;
    private readonly ToolStripMenuItem _miRefreshFiles;
    private readonly ToolStripSeparator _miSeparator;
    private readonly ToolStripMenuItem _miOpenFolder;
    private readonly ToolTip _toolTip;

    private ThemePalette _palette = ThemeManager.Dark;
    private PlatformCardModel? _model;
    private bool _isBusy;

    public event EventHandler<GamePlatform>? PrimaryActionClicked;
    public event EventHandler<GamePlatform>? CreateShortcutRequested;
    public event EventHandler<GamePlatform>? RefreshFilesRequested;
    public event EventHandler<GamePlatform>? OpenFolderRequested;

    public PlatformCardControl(GamePlatform platform, string displayName, string iconAsset, ToolTip toolTip)
    {
        Platform = platform;
        _toolTip = toolTip;

        AutoScaleMode = AutoScaleMode.Inherit;
        Dock = DockStyle.Fill;
        Margin = new Padding(0);
        Padding = new Padding(0);

        _card = new CardPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 9, 8, 8),
            Margin = new Padding(0),
            CornerRadius = 8
        };

        _layout = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 7,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _logo = new PictureBox
        {
            Image = AppAssets.LoadImage(iconAsset),
            SizeMode = PictureBoxSizeMode.Zoom,
            Height = 28,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 1, 0, 4),
            TabStop = false
        };

        _lblTitle = new Label
        {
            Text = displayName,
            Font = new Font("Segoe UI Semibold", 9.8F),
            TextAlign = ContentAlignment.MiddleCenter,
            UseMnemonic = false,
            AutoSize = true,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 3),
            TabStop = false
        };

        var pillHost = new BufferedPanel
        {
            BackColor = Color.Transparent,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 5)
        };
        _pillStatus = new StatusPill
        {
            Text = "Not set up",
            Font = new Font("Segoe UI Semibold", 8F),
            Anchor = AnchorStyles.None
        };
        pillHost.Controls.Add(_pillStatus);
        pillHost.SizeChanged += (_, _) => CenterChildHorizontally(pillHost, _pillStatus);
        _pillStatus.SizeChanged += (_, _) => CenterChildHorizontally(pillHost, _pillStatus);

        _lblVersion = new Label
        {
            Text = "Not set up",
            Font = new Font("Segoe UI", 8.5F),
            TextAlign = ContentAlignment.MiddleCenter,
            UseMnemonic = false,
            AutoSize = true,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 2),
            TabStop = false
        };

        _lblHint = new Label
        {
            Text = string.Empty,
            Font = new Font("Segoe UI", 8.2F),
            TextAlign = ContentAlignment.MiddleCenter,
            UseMnemonic = false,
            AutoSize = true,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 4),
            TabStop = false
        };

        var spacer = new BufferedPanel
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            MinimumSize = new Size(1, 4),
            TabStop = false
        };

        _actionsRow = new BufferedTableLayoutPanel
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        _actionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _actionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _actionsRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _btnPrimary = new ActionButton
        {
            Text = "Play Now",
            Font = new Font("Segoe UI Semibold", 8.8F),
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(4, 3, 4, 3),
            Margin = new Padding(0, 0, 3, 0),
            AccessibleName = $"{displayName} primary action"
        };
        _btnPrimary.Click += (_, _) => PrimaryActionClicked?.Invoke(this, Platform);

        _btnOverflow = new ActionButton
        {
            Text = "•••",
            Font = new Font("Segoe UI Semibold", 8.5F),
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0, 3, 0, 3),
            Margin = new Padding(0),
            AccessibleName = $"More options for {displayName}"
        };
        _toolTip.SetToolTip(_btnOverflow, $"More options for {displayName}");

        _overflowMenu = new ContextMenuStrip();
        _miShortcut = new ToolStripMenuItem("Create Desktop Shortcut");
        _miRefreshFiles = new ToolStripMenuItem("Refresh saved files");
        _miSeparator = new ToolStripSeparator();
        _miOpenFolder = new ToolStripMenuItem("Open game folder");

        _miShortcut.Click += (_, _) => CreateShortcutRequested?.Invoke(this, Platform);
        _miRefreshFiles.Click += (_, _) => RefreshFilesRequested?.Invoke(this, Platform);
        _miOpenFolder.Click += (_, _) => OpenFolderRequested?.Invoke(this, Platform);

        _overflowMenu.Items.AddRange([_miShortcut, _miRefreshFiles, _miSeparator, _miOpenFolder]);
        _btnOverflow.Click += (_, _) =>
        {
            if (_isBusy || _model is null)
            {
                return;
            }

            ThemeManager.StyleContextMenu(_overflowMenu, _palette);
            _overflowMenu.Show(_btnOverflow, new Point(0, _btnOverflow.Height + 2));
        };

        _actionsRow.Controls.Add(_btnPrimary, 0, 0);
        _actionsRow.Controls.Add(_btnOverflow, 1, 0);

        _layout.Controls.Add(_logo, 0, 0);
        _layout.Controls.Add(_lblTitle, 0, 1);
        _layout.Controls.Add(pillHost, 0, 2);
        _layout.Controls.Add(_lblVersion, 0, 3);
        _layout.Controls.Add(_lblHint, 0, 4);
        _layout.Controls.Add(spacer, 0, 5);
        _layout.Controls.Add(_actionsRow, 0, 6);

        _card.Controls.Add(_layout);
        Controls.Add(_card);

        ApplyInteractiveState();
        SizeChanged += (_, _) => UpdateTextWrappingBounds();
    }

    public GamePlatform Platform { get; }

    public void Bind(PlatformCardModel model)
    {
        _model = model;
        _card.IsHighlighted = model.IsActive;
        _lblTitle.Text = model.DisplayName;
        _pillStatus.SetState(model.StatusText, model.StatusTone);
        _lblVersion.Text = model.VersionText;
        _lblHint.Text = model.HintText;
        _lblHint.ForeColor = ResolveToneColor(model.HintTone);

        _btnPrimary.Text = model.PrimaryActionText;
        _btnPrimary.Variant = model.PrimaryVariant;
        _btnPrimary.ApplyTheme(_palette);

        _btnOverflow.Visible = model.ShowOverflowMenu;
        _btnPrimary.Margin = model.ShowOverflowMenu ? new Padding(0, 0, 3, 0) : new Padding(0);

        _miShortcut.Enabled = model.CanCreateShortcut;
        _miRefreshFiles.Enabled = model.CanRefreshFiles;
        _miOpenFolder.Enabled = model.CanOpenFolder;

        _toolTip.SetToolTip(_pillStatus, model.StatusTooltip);
        _toolTip.SetToolTip(_btnPrimary, model.PrimaryTooltip);

        ApplyInteractiveState();
        UpdateTextWrappingBounds();
    }

    public void ApplyTheme(ThemePalette palette)
    {
        _palette = palette;
        BackColor = palette.WindowBg;
        _card.ApplyTheme(palette);
        _lblTitle.ForeColor = palette.TextPrimary;
        _lblVersion.ForeColor = palette.TextSecondary;
        _lblHint.ForeColor = _model is not null ? ResolveToneColor(_model.HintTone) : palette.TextSecondary;
        _pillStatus.ApplyTheme(palette);
        _btnPrimary.ApplyTheme(palette);
        _btnOverflow.ApplyTheme(palette);
        ThemeManager.StyleContextMenu(_overflowMenu, palette);
    }

    public void SetBusy(bool busy)
    {
        _isBusy = busy;
        ApplyInteractiveState();
    }

    public void UpdateTextWrappingBounds(int fallbackCardWidth = 0)
    {
        var cardW = _card.ClientSize.Width > 0 ? _card.ClientSize.Width : fallbackCardWidth;
        var minInner = Math.Max(110, (int)Math.Ceiling(110 * (Math.Max(96, DeviceDpi) / 96f)));
        var innerW = Math.Max(minInner, cardW - _card.Padding.Horizontal - 4);
        _lblTitle.MaximumSize = new Size(innerW, 0);
        _lblVersion.MaximumSize = new Size(innerW, 0);
        _lblHint.MaximumSize = new Size(innerW, 0);
    }

    internal int GetMinimumContentHeight()
    {
        var pillH = Math.Max(_pillStatus.Height, _pillStatus.PreferredSize.Height);
        var btnH = Math.Max(_btnPrimary.PreferredSize.Height, _btnOverflow.PreferredSize.Height);
        var spacerMin = Math.Max(4, (int)Math.Ceiling(4 * (Math.Max(96, DeviceDpi) / 96f)));
        return Padding.Vertical
            + _card.Padding.Vertical
            + _logo.Height + _logo.Margin.Vertical
            + _lblTitle.PreferredSize.Height + _lblTitle.Margin.Vertical
            + pillH + _pillStatus.Margin.Vertical + _pillStatus.Parent!.Margin.Vertical
            + _lblVersion.PreferredSize.Height + _lblVersion.Margin.Vertical
            + _lblHint.PreferredSize.Height + _lblHint.Margin.Vertical
            + spacerMin
            + btnH
            + 4;
    }

    private void ApplyInteractiveState()
    {
        var primaryAllowed = !_isBusy && (_model?.PrimaryEnabled ?? false);
        var overflowAllowed = !_isBusy && (_model?.ShowOverflowMenu ?? false) &&
            ((_model?.CanCreateShortcut ?? false) ||
             (_model?.CanRefreshFiles ?? false) ||
             (_model?.CanOpenFolder ?? false));

        _btnPrimary.Enabled = primaryAllowed;
        _btnOverflow.Enabled = overflowAllowed;
    }

    private Color ResolveToneColor(StatusTone tone) => tone switch
    {
        StatusTone.Accent => _palette.AccentText,
        StatusTone.Success => _palette.SuccessText,
        StatusTone.Warning => _palette.WarningText,
        StatusTone.Danger => _palette.DangerText,
        _ => _palette.TextSecondary
    };

    private static void CenterChildHorizontally(Control container, Control child)
    {
        var x = Math.Max(0, (container.ClientSize.Width - child.Width) / 2);
        var y = 0;
        if (child.Location.X != x || child.Location.Y != y)
        {
            child.Location = new Point(x, y);
        }

        if (container.Height != child.Height)
        {
            container.Height = child.Height;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _logo.Image?.Dispose();
            _logo.Image = null;
            _overflowMenu.Dispose();
        }
        base.Dispose(disposing);
    }

    internal ActionButton PrimaryButtonForTest => _btnPrimary;
    internal ActionButton OverflowButtonForTest => _btnOverflow;
    internal StatusPill StatusPillForTest => _pillStatus;
    internal CardPanel CardPanelForTest => _card;
    internal ContextMenuStrip OverflowMenuForTest => _overflowMenu;
    internal ToolStripMenuItem ShortcutMenuItemForTest => _miShortcut;
    internal ToolStripMenuItem RefreshFilesMenuItemForTest => _miRefreshFiles;
    internal ToolStripMenuItem OpenFolderMenuItemForTest => _miOpenFolder;
}
