using System.Drawing;
using System.Windows.Forms;
using Grand_Theft_Auto_V_Platform_Switcher.Models;
using Grand_Theft_Auto_V_Platform_Switcher.Services;
using Grand_Theft_Auto_V_Platform_Switcher.UI.Controls;

namespace Grand_Theft_Auto_V_Platform_Switcher.UI.Views;

public sealed class EpicSetupDialog : Form
{
    private readonly EditionSpec _edition;
    private readonly ProcessGuard _processGuard;
    private readonly ThemePalette _palette;

    private readonly BufferedTableLayoutPanel _root;
    private readonly Label _lblTitle;
    private readonly Label _lblSubtitle;
    private readonly Label _lblStep1;
    private readonly Label _lblStep2Title;
    private readonly StatusPill _pillEpicState;
    private readonly ActionButton _btnRecheck;
    private readonly Label _lblStep3;
    private readonly TextBox _txtFolder;
    private readonly ActionButton _btnBrowse;
    private readonly Label _lblHint;
    private readonly ActionButton _btnCancel;
    private readonly ActionButton _btnAdopt;

    public string SelectedTempFolder { get; private set; } = string.Empty;

    public EpicSetupDialog(EditionSpec edition, ProcessGuard processGuard, ThemePalette palette)
    {
        _edition = edition;
        _processGuard = processGuard;
        _palette = palette;

        SuspendLayout();
        Text = $"Set Up Epic Games — {edition.DisplayName}";
        Icon = AppAssets.LoadAppIcon();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(520, 325);
        Font = new Font("Segoe UI", 9F);

        _root = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(14, 12, 14, 12),
            Margin = new Padding(0)
        };
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _lblTitle = new Label
        {
            Text = "Link Epic Games Without Re-Downloading",
            Font = new Font("Segoe UI Semibold", 10.5F),
            UseMnemonic = false,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2),
            TabStop = false
        };

        _lblSubtitle = new Label
        {
            Text = $"Use your existing {edition.DisplayName} installation with Epic Games Launcher.",
            Font = new Font("Segoe UI", 8.5F),
            UseMnemonic = false,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10),
            TabStop = false
        };

        _lblStep1 = new Label
        {
            Text = $"1. Open Epic Games Launcher, start installing {edition.DisplayName} into any empty temp folder for 5–10 seconds, then Pause and Quit Epic completely (including system tray).",
            Font = new Font("Segoe UI", 8.6F),
            UseMnemonic = false,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8),
            TabStop = false
        };

        var step2Row = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(0)
        };
        step2Row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        step2Row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        step2Row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _lblStep2Title = new Label
        {
            Text = "2. Epic Launcher status:",
            Font = new Font("Segoe UI", 8.6F),
            UseMnemonic = false,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 8, 0),
            TabStop = false
        };
        _pillEpicState = new StatusPill
        {
            Font = new Font("Segoe UI Semibold", 8F),
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0)
        };
        _btnRecheck = new ActionButton
        {
            Text = "Re-check",
            Font = new Font("Segoe UI", 8.3F),
            AutoSize = true,
            Padding = new Padding(6, 2, 6, 2),
            Anchor = AnchorStyles.Right,
            AccessibleName = "Re-check if Epic Games Launcher is closed"
        };
        _btnRecheck.Click += (_, _) => RefreshState();

        step2Row.Controls.Add(_lblStep2Title, 0, 0);
        step2Row.Controls.Add(_pillEpicState, 1, 0);
        step2Row.Controls.Add(_btnRecheck, 2, 0);

        var step3Panel = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 3,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        step3Panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        step3Panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _lblStep3 = new Label
        {
            Text = "3. Choose that temporary Epic folder (containing '.egstore'):",
            Font = new Font("Segoe UI", 8.6F),
            UseMnemonic = false,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4),
            TabStop = false
        };
        step3Panel.SetColumnSpan(_lblStep3, 2);

        _txtFolder = new TextBox
        {
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle,
            Dock = DockStyle.Fill,
            PlaceholderText = "Select folder containing .egstore...",
            Margin = new Padding(0, 2, 6, 2)
        };
        _btnBrowse = new ActionButton
        {
            Text = "Browse...",
            Font = new Font("Segoe UI", 8.5F),
            AutoSize = true,
            Padding = new Padding(8, 2, 8, 2),
            Anchor = AnchorStyles.Right
        };
        _btnBrowse.Click += (_, _) => BrowseTempFolder();

        _lblHint = new Label
        {
            Text = "Your current launcher files will be backed up automatically before linking.",
            Font = new Font("Segoe UI", 8.1F),
            UseMnemonic = false,
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 0),
            TabStop = false
        };
        step3Panel.SetColumnSpan(_lblHint, 2);

        step3Panel.Controls.Add(_lblStep3, 0, 0);
        step3Panel.Controls.Add(_txtFolder, 0, 1);
        step3Panel.Controls.Add(_btnBrowse, 1, 1);
        step3Panel.Controls.Add(_lblHint, 0, 2);

        var footer = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 8, 0, 0),
            Padding = new Padding(0)
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _btnCancel = new ActionButton
        {
            Text = "Cancel",
            Font = new Font("Segoe UI", 8.8F),
            AutoSize = true,
            Padding = new Padding(10, 3, 10, 3),
            Margin = new Padding(0, 0, 6, 0),
            DialogResult = DialogResult.Cancel
        };
        _btnAdopt = new ActionButton
        {
            Text = "Link Epic Folder",
            Font = new Font("Segoe UI Semibold", 8.8F),
            Variant = ActionButtonVariant.PrimaryAccent,
            AutoSize = true,
            Padding = new Padding(10, 3, 10, 3),
            Margin = new Padding(0),
            DialogResult = DialogResult.OK
        };

        footer.Controls.Add(new Panel { Size = new Size(1, 1), Margin = new Padding(0) }, 0, 0);
        footer.Controls.Add(_btnCancel, 1, 0);
        footer.Controls.Add(_btnAdopt, 2, 0);

        _root.Controls.Add(_lblTitle, 0, 0);
        _root.Controls.Add(_lblSubtitle, 0, 1);
        _root.Controls.Add(_lblStep1, 0, 2);
        _root.Controls.Add(step2Row, 0, 3);
        _root.Controls.Add(step3Panel, 0, 4);
        _root.Controls.Add(footer, 0, 5);

        Controls.Add(_root);
        AcceptButton = _btnAdopt;
        CancelButton = _btnCancel;

        ResumeLayout(true);
        ApplyTheme();
        RefreshState();
        UpdateWrappingBounds();

        SizeChanged += (_, _) => UpdateWrappingBounds();
        HandleCreated += (_, _) => ThemeManager.ApplyTitleBarTheme(this, _palette.IsDark);
    }

    private void UpdateWrappingBounds()
    {
        var maxW = Math.Max(240, ClientSize.Width - _root.Padding.Horizontal - 4);
        _lblTitle.MaximumSize = new Size(maxW, 0);
        _lblSubtitle.MaximumSize = new Size(maxW, 0);
        _lblStep1.MaximumSize = new Size(maxW, 0);
        _lblStep3.MaximumSize = new Size(maxW, 0);
        _lblHint.MaximumSize = new Size(maxW, 0);
    }

    private void ApplyTheme()
    {
        BackColor = _palette.WindowBg;
        ForeColor = _palette.TextPrimary;
        _lblTitle.ForeColor = _palette.TextPrimary;
        _lblSubtitle.ForeColor = _palette.TextSecondary;
        _lblStep1.ForeColor = _palette.TextPrimary;
        _lblStep2Title.ForeColor = _palette.TextPrimary;
        _lblStep3.ForeColor = _palette.TextPrimary;
        _lblHint.ForeColor = _palette.TextSecondary;
        _txtFolder.BackColor = _palette.InputBg;
        _txtFolder.ForeColor = _palette.TextPrimary;

        _pillEpicState.ApplyTheme(_palette);
        _btnRecheck.ApplyTheme(_palette);
        _btnBrowse.ApplyTheme(_palette);
        _btnCancel.ApplyTheme(_palette);
        _btnAdopt.ApplyTheme(_palette);
    }

    private void BrowseTempFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = $"Select temporary Epic Games folder for {_edition.DisplayName} (containing .egstore)",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(SelectedTempFolder) ? SelectedTempFolder : string.Empty
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            SelectedTempFolder = dialog.SelectedPath;
            _txtFolder.Text = SelectedTempFolder;
            RefreshState();
        }
    }

    internal void RefreshState()
    {
        var epicRunning = _processGuard.IsEpicGamesLauncherRunning();
        if (epicRunning)
        {
            _pillEpicState.SetState("Epic Launcher is running", StatusTone.Warning);
        }
        else
        {
            _pillEpicState.SetState("Epic Launcher closed", StatusTone.Success);
        }

        var trimmedFolder = SelectedTempFolder.TrimEnd('\\', '/');
        var candidateParent = string.Equals(Path.GetFileName(trimmedFolder), ".egstore", StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(trimmedFolder)
            : trimmedFolder;
        var hasEgstore = !string.IsNullOrWhiteSpace(candidateParent) &&
                         EpicManifestService.HasEgstoreDirectory(candidateParent);

        if (!string.IsNullOrWhiteSpace(SelectedTempFolder) && !hasEgstore)
        {
            _lblHint.Text = "Selected folder does not contain an '.egstore' subfolder yet.";
            _lblHint.ForeColor = _palette.WarningText;
        }
        else
        {
            _lblHint.Text = "Your current launcher files will be backed up automatically before linking.";
            _lblHint.ForeColor = _palette.TextSecondary;
        }

        _btnAdopt.Enabled = !epicRunning && hasEgstore;
    }

    internal ActionButton LinkButtonForTest => _btnAdopt;
    internal ActionButton CancelButtonForTest => _btnCancel;
    internal StatusPill EpicStatePillForTest => _pillEpicState;
}
