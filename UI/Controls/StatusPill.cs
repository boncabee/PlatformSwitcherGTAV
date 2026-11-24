using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Grand_Theft_Auto_V_Platform_Switcher.UI.Controls;

public enum StatusTone
{
    Neutral,
    Accent,
    Success,
    Warning,
    Danger
}

public sealed class StatusPill : Label
{
    private ThemePalette _palette = ThemeManager.Dark;
    private StatusTone _tone = StatusTone.Neutral;

    public StatusPill()
    {
        DoubleBuffered = true;
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        AutoSize = true;
        UseMnemonic = false;
        TabStop = false;
        TextAlign = ContentAlignment.MiddleCenter;
        Margin = new Padding(0);
    }

    public StatusTone Tone
    {
        get => _tone;
        set
        {
            if (_tone != value)
            {
                _tone = value;
                SyncColors();
                Invalidate();
            }
        }
    }

    public void ApplyTheme(ThemePalette palette)
    {
        _palette = palette;
        SyncColors();
        Invalidate();
    }

    public void SetState(string text, StatusTone tone)
    {
        _tone = tone;
        Text = text;
        SyncColors();
        Invalidate();
    }

    private void SyncColors()
    {
        var (bg, fg) = ResolveToneColors();
        BackColor = bg;
        ForeColor = fg;
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var text = string.IsNullOrEmpty(Text) ? " " : Text;
        var textSize = TextRenderer.MeasureText(text, Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        var padX = Math.Max(14, (int)Math.Ceiling(Font.Height * 0.9f));
        var padY = Math.Max(4, (int)Math.Ceiling(Font.Height * 0.25f));
        return new Size(
            Math.Max(MinimumSize.Width, textSize.Width + padX),
            Math.Max(MinimumSize.Height, textSize.Height + padY));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var parentBg = ThemeManager.ResolveOpaqueBackground(Parent, _palette.SurfaceBg);
        g.Clear(parentBg);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var (pillBg, pillFg) = ResolveToneColors();
        var rect = new RectangleF(0.5f, 0.5f, Math.Max(1f, Width - 1f), Math.Max(1f, Height - 1f));
        var radius = Math.Max(4f, rect.Height / 2f);

        using (var path = ThemeManager.CreateRoundedRectPath(rect, radius))
        using (var brush = new SolidBrush(pillBg))
        {
            g.FillPath(brush, path);
        }

        var flags = TextFormatFlags.SingleLine
            | TextFormatFlags.HorizontalCenter
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.NoPrefix;
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, pillFg, flags);
    }

    internal (Color Bg, Color Fg) ResolveToneColors() => _tone switch
    {
        StatusTone.Accent => (_palette.AccentSubtleBg, _palette.AccentText),
        StatusTone.Success => (_palette.SuccessSubtleBg, _palette.SuccessText),
        StatusTone.Warning => (_palette.WarningSubtleBg, _palette.WarningText),
        StatusTone.Danger => (_palette.DangerSubtleBg, _palette.DangerText),
        _ => (_palette.ButtonBg, _palette.TextSecondary)
    };
}
