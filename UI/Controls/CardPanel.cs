using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Grand_Theft_Auto_V_Platform_Switcher.UI.Controls;

public sealed class CardPanel : BufferedPanel
{
    private ThemePalette _palette = ThemeManager.Dark;
    private bool _isHighlighted;

    public bool IsHighlighted
    {
        get => _isHighlighted;
        set
        {
            if (_isHighlighted != value)
            {
                _isHighlighted = value;
                BackColor = ResolveCardBackColor();
                Invalidate();
            }
        }
    }

    public int CornerRadius { get; set; } = 8;

    public void ApplyTheme(ThemePalette palette)
    {
        _palette = palette;
        BackColor = ResolveCardBackColor();
        ForeColor = palette.TextPrimary;
        Invalidate();
    }

    private Color ResolveCardBackColor() => _isHighlighted ? _palette.CardActiveBg : _palette.CardBg;

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics;
        var parentBg = ThemeManager.ResolveOpaqueBackground(Parent, _palette.WindowBg);
        g.Clear(parentBg);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var scale = Math.Max(96, DeviceDpi) / 96f;
        var radius = CornerRadius * scale;
        var bounds = new RectangleF(0.5f, 0.5f, Math.Max(1f, Width - 1f), Math.Max(1f, Height - 1f));

        using var cardPath = ThemeManager.CreateRoundedRectPath(bounds, radius);
        using (var bgBrush = new SolidBrush(ResolveCardBackColor()))
        {
            g.FillPath(bgBrush, cardPath);
        }

        if (_isHighlighted)
        {
            var barHeight = Math.Max(3f, 3f * scale);
            var prevClip = g.Clip;
            try
            {
                using var region = new Region(cardPath);
                g.Clip = region;
                using var barBrush = new SolidBrush(_palette.Accent);
                g.FillRectangle(barBrush, 0f, 0f, Width, barHeight);
            }
            finally
            {
                g.Clip = prevClip;
            }
        }

        var borderThickness = _isHighlighted ? 1.75f : 1f;
        var borderColor = _isHighlighted ? _palette.Accent : _palette.Border;
        var inset = borderThickness / 2f;
        var borderBounds = new RectangleF(inset, inset, Math.Max(1f, Width - borderThickness), Math.Max(1f, Height - borderThickness));
        using var borderPath = ThemeManager.CreateRoundedRectPath(borderBounds, radius);
        using var pen = new Pen(borderColor, borderThickness);
        g.DrawPath(pen, borderPath);
    }
}
