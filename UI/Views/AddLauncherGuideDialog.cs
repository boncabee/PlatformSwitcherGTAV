using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Grand_Theft_Auto_V_Platform_Switcher.Models;
using Grand_Theft_Auto_V_Platform_Switcher.UI.Controls;

namespace Grand_Theft_Auto_V_Platform_Switcher.UI.Views;

public sealed class AddLauncherGuideDialog : Form
{
    private readonly ThemePalette _palette;
    private readonly string _gameDirectory;
    private readonly BufferedTableLayoutPanel _root;
    private readonly Label _lblTitle;
    private readonly Label _lblSteps;
    private readonly Label _lblNote;
    private readonly ActionButton _btnOpenFolder;
    private readonly ActionButton _btnClose;

    public AddLauncherGuideDialog(
        EditionSpec edition,
        GamePlatform targetPlatform,
        string gameDirectory,
        ThemePalette palette)
    {
        _palette = palette;
        _gameDirectory = gameDirectory;

        SuspendLayout();
        var platformName = EditionSpec.GetPlatformDisplayName(targetPlatform);
        Text = $"Add {platformName} — {edition.DisplayName}";
        Icon = AppAssets.LoadAppIcon();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(470, 225);
        Font = new Font("Segoe UI", 9F);

        _root = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(14, 12, 14, 12),
            Margin = new Padding(0)
        };
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _lblTitle = new Label
        {
            Text = $"How to Add {platformName}",
            Font = new Font("Segoe UI Semibold", 10.5F),
            ForeColor = palette.TextPrimary,
            UseMnemonic = false,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8),
            TabStop = false
        };

        var stepsText = targetPlatform == GamePlatform.Steam
            ? $"1. Open Steam and point or verify {edition.DisplayName} using your existing game folder.\n" +
              "2. Return to this app — your Steam launcher files will be saved automatically so you can switch back and forth in 1 click."
            : $"1. Open Rockstar Games Launcher and scan or point {edition.DisplayName} to your existing game folder.\n" +
              "2. Return to this app — your Rockstar launcher files will be saved automatically so you can switch back and forth in 1 click.";

        _lblSteps = new Label
        {
            Text = stepsText,
            Font = new Font("Segoe UI", 8.8F),
            ForeColor = palette.TextPrimary,
            UseMnemonic = false,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6),
            TabStop = false
        };

        _lblNote = new Label
        {
            Text = "Your currently active launcher files stay backed up safely.",
            Font = new Font("Segoe UI", 8.2F),
            ForeColor = palette.TextSecondary,
            UseMnemonic = false,
            AutoSize = true,
            Margin = new Padding(0),
            TabStop = false
        };

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

        _btnOpenFolder = new ActionButton
        {
            Text = "Open Game Folder",
            Font = new Font("Segoe UI", 8.6F),
            AutoSize = true,
            Padding = new Padding(8, 3, 8, 3),
            Margin = new Padding(0, 0, 6, 0),
            Enabled = !string.IsNullOrWhiteSpace(_gameDirectory) && Directory.Exists(_gameDirectory)
        };
        _btnOpenFolder.ApplyTheme(palette);
        _btnOpenFolder.Click += (_, _) => OpenGameFolder();

        _btnClose = new ActionButton
        {
            Text = "Got It",
            Font = new Font("Segoe UI Semibold", 8.8F),
            Variant = ActionButtonVariant.PrimaryAccent,
            AutoSize = true,
            Padding = new Padding(12, 3, 12, 3),
            Margin = new Padding(0),
            DialogResult = DialogResult.OK
        };
        _btnClose.ApplyTheme(palette);

        footer.Controls.Add(new Panel { Size = new Size(1, 1), Margin = new Padding(0) }, 0, 0);
        footer.Controls.Add(_btnOpenFolder, 1, 0);
        footer.Controls.Add(_btnClose, 2, 0);

        _root.Controls.Add(_lblTitle, 0, 0);
        _root.Controls.Add(_lblSteps, 0, 1);
        _root.Controls.Add(_lblNote, 0, 2);
        _root.Controls.Add(footer, 0, 3);

        Controls.Add(_root);
        BackColor = palette.WindowBg;
        ForeColor = palette.TextPrimary;
        AcceptButton = _btnClose;
        CancelButton = _btnClose;

        ResumeLayout(true);
        UpdateWrappingBounds();

        SizeChanged += (_, _) => UpdateWrappingBounds();
        HandleCreated += (_, _) => ThemeManager.ApplyTitleBarTheme(this, _palette.IsDark);
    }

    private void UpdateWrappingBounds()
    {
        var maxW = Math.Max(220, ClientSize.Width - _root.Padding.Horizontal - 4);
        _lblTitle.MaximumSize = new Size(maxW, 0);
        _lblSteps.MaximumSize = new Size(maxW, 0);
        _lblNote.MaximumSize = new Size(maxW, 0);
    }

    private void OpenGameFolder()
    {
        try
        {
            if (Directory.Exists(_gameDirectory))
            {
                Process.Start(new ProcessStartInfo(_gameDirectory) { UseShellExecute = true });
            }
        }
        catch
        {
            // Ignore explorer launch errors.
        }
    }
}
