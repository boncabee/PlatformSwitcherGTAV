using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Grand_Theft_Auto_V_Platform_Switcher.UI.Controls;

public sealed class SegmentedControl : BufferedTableLayoutPanel
{
    private readonly ActionButton _btnLeft;
    private readonly ActionButton _btnRight;
    private ThemePalette _palette = ThemeManager.Dark;
    private int _selectedIndex;

    public event EventHandler<int>? SelectedIndexChanged;

    public SegmentedControl(string leftText, string rightText)
    {
        ColumnCount = 2;
        RowCount = 1;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(3);
        Margin = new Padding(0);
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _btnLeft = new ActionButton
        {
            Text = leftText,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 2, 8, 2),
            Margin = new Padding(0, 0, 2, 0),
            CornerRadius = 5
        };
        _btnRight = new ActionButton
        {
            Text = rightText,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 2, 8, 2),
            Margin = new Padding(2, 0, 0, 0),
            CornerRadius = 5
        };

        _btnLeft.Click += (_, _) => SetSelectedIndex(0, raiseEvent: true);
        _btnRight.Click += (_, _) => SetSelectedIndex(1, raiseEvent: true);

        Controls.Add(_btnLeft, 0, 0);
        Controls.Add(_btnRight, 1, 0);
        UpdateSelectionStyles();
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set => SetSelectedIndex(value, raiseEvent: false);
    }

    public ActionButton LeftButton => _btnLeft;
    public ActionButton RightButton => _btnRight;

    public void SetSegmentTexts(string leftText, string rightText)
    {
        _btnLeft.Text = leftText;
        _btnRight.Text = rightText;
    }

    public void SetTooltips(ToolTip toolTip, string leftTip, string rightTip)
    {
        toolTip.SetToolTip(_btnLeft, leftTip);
        toolTip.SetToolTip(_btnRight, rightTip);
    }

    public void ApplyTheme(ThemePalette palette)
    {
        _palette = palette;
        BackColor = palette.InputBg;
        _btnLeft.ApplyTheme(palette);
        _btnRight.ApplyTheme(palette);
        UpdateSelectionStyles();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var scale = Math.Max(96, DeviceDpi) / 96f;
        var rect = new RectangleF(0.5f, 0.5f, Math.Max(1f, Width - 1f), Math.Max(1f, Height - 1f));
        using var path = ThemeManager.CreateRoundedRectPath(rect, 7f * scale);
        using var pen = new Pen(_palette.Border, 1f);
        g.DrawPath(pen, path);
    }

    private void SetSelectedIndex(int index, bool raiseEvent)
    {
        var clamped = index == 1 ? 1 : 0;
        if (_selectedIndex == clamped)
        {
            return;
        }

        _selectedIndex = clamped;
        UpdateSelectionStyles();
        if (raiseEvent)
        {
            SelectedIndexChanged?.Invoke(this, _selectedIndex);
        }
    }

    private void UpdateSelectionStyles()
    {
        _btnLeft.Variant = _selectedIndex == 0 ? ActionButtonVariant.PrimaryAccent : ActionButtonVariant.NavTab;
        _btnRight.Variant = _selectedIndex == 1 ? ActionButtonVariant.PrimaryAccent : ActionButtonVariant.NavTab;
    }
}
