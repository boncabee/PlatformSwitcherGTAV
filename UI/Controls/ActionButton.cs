using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Grand_Theft_Auto_V_Platform_Switcher.UI.Controls;

public enum ActionButtonVariant
{
    Secondary,
    PrimarySuccess,
    PrimaryAccent,
    Danger,
    NavTab,
    NavTabActive,
    SubtleLink
}

public sealed class ActionButton : Button
{
    private ThemePalette _palette = ThemeManager.Dark;
    private ActionButtonVariant _variant = ActionButtonVariant.Secondary;
    private bool _hovered;
    private bool _pressed;

    public ActionButton()
    {
        DoubleBuffered = true;
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        UseMnemonic = false;
        Cursor = Cursors.Hand;
        Margin = new Padding(0);
    }

    public ActionButtonVariant Variant
    {
        get => _variant;
        set
        {
            if (_variant != value)
            {
                _variant = value;
                SyncSystemColors();
                Invalidate();
            }
        }
    }

    public int CornerRadius { get; set; } = 6;

    public void ApplyTheme(ThemePalette palette)
    {
        _palette = palette;
        SyncSystemColors();
        Invalidate();
    }

    private void SyncSystemColors()
    {
        BackColor = ThemeManager.ResolveOpaqueBackground(Parent, _palette.SurfaceBg);
        ForeColor = ResolveForegroundColor(Enabled);
    }

    protected override void OnParentBackColorChanged(EventArgs e)
    {
        base.OnParentBackColorChanged(e);
        if (Parent is not null)
        {
            BackColor = ThemeManager.ResolveOpaqueBackground(Parent, _palette.SurfaceBg);
            Invalidate();
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = false;
        _pressed = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        base.OnMouseDown(mevent);
        if (mevent.Button == MouseButtons.Left)
        {
            _pressed = true;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        base.OnMouseUp(mevent);
        _pressed = false;
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        ForeColor = ResolveForegroundColor(Enabled);
        Invalidate();
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var text = string.IsNullOrEmpty(Text) ? " " : Text;
        var singleText = TextRenderer.MeasureText(text, Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        var extraW = Math.Max(12, (int)Math.Ceiling(Font.Height * 0.75f));
        var extraH = Math.Max(8, (int)Math.Ceiling(Font.Height * 0.45f));
        var width = singleText.Width + Padding.Horizontal + extraW;
        var height = singleText.Height + Padding.Vertical + extraH;
        return new Size(Math.Max(width, MinimumSize.Width), Math.Max(height, MinimumSize.Height));
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        var parentBg = ThemeManager.ResolveOpaqueBackground(Parent, _palette.SurfaceBg);
        g.Clear(parentBg);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var bounds = new RectangleF(1f, 1f, Math.Max(1f, Width - 2f), Math.Max(1f, Height - 2f));
        var radius = ScalePx(CornerRadius);
        var (fill, border) = ResolveBackgroundAndBorder();
        var fg = ResolveForegroundColor(Enabled);

        if (fill != Color.Transparent)
        {
            using var fillPath = ThemeManager.CreateRoundedRectPath(bounds, radius);
            using var brush = new SolidBrush(fill);
            g.FillPath(brush, fillPath);
        }

        if (border != Color.Transparent)
        {
            using var borderPath = ThemeManager.CreateRoundedRectPath(bounds, radius);
            using var pen = new Pen(border, 1f);
            g.DrawPath(pen, borderPath);
        }

        if (_variant == ActionButtonVariant.NavTabActive)
        {
            var barH = Math.Max(2f, ScalePx(2));
            var barInset = Math.Min(ScalePx(10), bounds.Width / 4f);
            using var barBrush = new SolidBrush(_palette.AccentText);
            g.FillRectangle(barBrush, bounds.X + barInset, Height - barH - 1f, Math.Max(4f, bounds.Width - barInset * 2f), barH);
        }

        if (Focused && ShowFocusCues && Enabled)
        {
            var focusRect = new RectangleF(0.75f, 0.75f, Math.Max(1f, Width - 1.5f), Math.Max(1f, Height - 1.5f));
            using var focusPath = ThemeManager.CreateRoundedRectPath(focusRect, radius + 1f);
            using var focusPen = new Pen(_palette.FocusRing, 1.75f);
            g.DrawPath(focusPen, focusPath);
        }

        var textBounds = new Rectangle(
            Padding.Left + 2,
            Padding.Top,
            Math.Max(1, Width - Padding.Horizontal - 4),
            Math.Max(1, Height - Padding.Vertical));

        var flags = TextFormatFlags.SingleLine
            | TextFormatFlags.HorizontalCenter
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.NoPrefix
            | TextFormatFlags.EndEllipsis;

        TextRenderer.DrawText(g, Text, Font, textBounds, fg, flags);
    }

    private (Color Fill, Color Border) ResolveBackgroundAndBorder()
    {
        if (!Enabled)
        {
            return _variant switch
            {
                ActionButtonVariant.NavTab or ActionButtonVariant.NavTabActive or ActionButtonVariant.SubtleLink
                    => (Color.Transparent, Color.Transparent),
                _ => (_palette.SurfaceBg, _palette.Border)
            };
        }

        var active = _hovered || _pressed;
        return _variant switch
        {
            ActionButtonVariant.PrimarySuccess => (
                active ? _palette.SuccessHover : _palette.Success,
                Color.Transparent),
            ActionButtonVariant.PrimaryAccent => (
                active ? _palette.AccentHover : _palette.Accent,
                Color.Transparent),
            ActionButtonVariant.Danger => (
                active ? _palette.DangerHover : _palette.Danger,
                Color.Transparent),
            ActionButtonVariant.NavTabActive => (
                active ? _palette.CardHoverBg : _palette.AccentSubtleBg,
                Color.Transparent),
            ActionButtonVariant.NavTab => (
                active ? _palette.CardHoverBg : Color.Transparent,
                Color.Transparent),
            ActionButtonVariant.SubtleLink => (
                active ? _palette.CardHoverBg : Color.Transparent,
                Color.Transparent),
            _ => (
                active ? _palette.ButtonHoverBg : _palette.ButtonBg,
                _palette.BorderStrong)
        };
    }

    private Color ResolveForegroundColor(bool isEnabled)
    {
        if (!isEnabled)
        {
            return _palette.TextDisabled;
        }

        return _variant switch
        {
            ActionButtonVariant.PrimarySuccess or ActionButtonVariant.PrimaryAccent or ActionButtonVariant.Danger => Color.White,
            ActionButtonVariant.NavTabActive => _palette.AccentText,
            ActionButtonVariant.SubtleLink => _palette.WarningText,
            _ => _palette.TextPrimary
        };
    }

    private float ScalePx(int px96) => px96 * (Math.Max(96, DeviceDpi) / 96f);
}
