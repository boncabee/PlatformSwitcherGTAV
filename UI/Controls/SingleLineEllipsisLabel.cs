using System.Drawing;
using System.Windows.Forms;

namespace Grand_Theft_Auto_V_Platform_Switcher.UI.Controls;

public sealed class SingleLineEllipsisLabel : Label
{
    public SingleLineEllipsisLabel()
    {
        AutoSize = false;
        AutoEllipsis = true;
        UseMnemonic = false;
        TabStop = false;
        TextAlign = ContentAlignment.MiddleLeft;
        SyncSingleLineHeight();
    }

    public bool UsePathEllipsis { get; set; }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        SyncSingleLineHeight();
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var h = ComputeSingleLineHeight();
        var textW = TextRenderer.MeasureText(
            string.IsNullOrEmpty(Text) ? " " : Text,
            Font,
            Size.Empty,
            TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width;
        var w = proposedSize.Width > 0 ? Math.Min(proposedSize.Width, textW) : textW;
        return new Size(w, h);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var bg = ThemeManager.ResolveOpaqueBackground(Parent, BackColor);
        e.Graphics.Clear(bg);
        var flags = TextFormatFlags.SingleLine
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.Left
            | TextFormatFlags.NoPrefix
            | (UsePathEllipsis ? TextFormatFlags.PathEllipsis : TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor, flags);
    }

    private void SyncSingleLineHeight()
    {
        var h = ComputeSingleLineHeight();
        MaximumSize = new Size(0, h);
        MinimumSize = new Size(0, h);
        Height = h;
    }

    private int ComputeSingleLineHeight() =>
        Font.Height + Math.Max(4, (int)Math.Ceiling(Font.Height * 0.25f));
}
