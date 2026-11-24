using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Grand_Theft_Auto_V_Platform_Switcher.UI;

public sealed record ThemePalette(
    bool IsDark,
    Color WindowBg,
    Color SurfaceBg,
    Color CardBg,
    Color CardActiveBg,
    Color CardHoverBg,
    Color Border,
    Color BorderStrong,
    Color Accent,
    Color AccentHover,
    Color AccentText,
    Color AccentSubtleBg,
    Color Success,
    Color SuccessHover,
    Color SuccessText,
    Color SuccessSubtleBg,
    Color Warning,
    Color WarningText,
    Color WarningSubtleBg,
    Color Danger,
    Color DangerHover,
    Color DangerText,
    Color DangerSubtleBg,
    Color TextPrimary,
    Color TextSecondary,
    Color TextDisabled,
    Color InputBg,
    Color ButtonBg,
    Color ButtonHoverBg,
    Color FocusRing);

public static class ThemeManager
{
    public static readonly ThemePalette Dark = new(
        IsDark: true,
        WindowBg: Color.FromArgb(17, 20, 25),           // #111419
        SurfaceBg: Color.FromArgb(24, 28, 35),          // #181C23
        CardBg: Color.FromArgb(30, 35, 45),             // #1E232D
        CardActiveBg: Color.FromArgb(25, 38, 56),       // #192638
        CardHoverBg: Color.FromArgb(38, 45, 59),        // #262D3B
        Border: Color.FromArgb(50, 58, 73),             // #323A49
        BorderStrong: Color.FromArgb(100, 116, 139),    // #64748B
        Accent: Color.FromArgb(29, 104, 224),           // #1D68E0 (5.12:1 w/ white)
        AccentHover: Color.FromArgb(24, 88, 194),       // #1858C2
        AccentText: Color.FromArgb(108, 180, 255),      // #6CB4FF
        AccentSubtleBg: Color.FromArgb(22, 40, 66),     // #162842
        Success: Color.FromArgb(27, 127, 55),           // #1B7F37 (5.09:1 w/ white)
        SuccessHover: Color.FromArgb(23, 109, 47),      // #176D2F
        SuccessText: Color.FromArgb(74, 222, 128),      // #4ADE80
        SuccessSubtleBg: Color.FromArgb(19, 42, 28),    // #132A1C
        Warning: Color.FromArgb(154, 103, 0),           // #9A6700 (4.86:1 w/ white)
        WarningText: Color.FromArgb(251, 191, 36),      // #FBBF24
        WarningSubtleBg: Color.FromArgb(46, 34, 9),     // #2E2209
        Danger: Color.FromArgb(207, 34, 46),            // #CF222E (5.36:1 w/ white)
        DangerHover: Color.FromArgb(182, 29, 40),       // #B61D28
        DangerText: Color.FromArgb(255, 123, 114),      // #FF7B72
        DangerSubtleBg: Color.FromArgb(50, 22, 26),     // #32161A
        TextPrimary: Color.FromArgb(238, 242, 246),     // #EEF2F6
        TextSecondary: Color.FromArgb(162, 174, 191),   // #A2AEBF
        TextDisabled: Color.FromArgb(110, 122, 140),    // #6E7A8C
        InputBg: Color.FromArgb(19, 23, 30),            // #13171E
        ButtonBg: Color.FromArgb(38, 45, 58),           // #262D3A
        ButtonHoverBg: Color.FromArgb(50, 59, 77),      // #323B4D
        FocusRing: Color.FromArgb(108, 180, 255));      // #6CB4FF

    public static readonly ThemePalette Light = new(
        IsDark: false,
        WindowBg: Color.FromArgb(243, 245, 248),        // #F3F5F8
        SurfaceBg: Color.FromArgb(255, 255, 255),       // #FFFFFF
        CardBg: Color.FromArgb(255, 255, 255),          // #FFFFFF
        CardActiveBg: Color.FromArgb(238, 245, 255),    // #EEF5FF
        CardHoverBg: Color.FromArgb(246, 248, 251),     // #F6F8FB
        Border: Color.FromArgb(203, 213, 225),          // #CBD5E1
        BorderStrong: Color.FromArgb(125, 140, 159),    // #7D8C9F
        Accent: Color.FromArgb(9, 105, 218),            // #0969DA (5.19:1 w/ white)
        AccentHover: Color.FromArgb(7, 87, 184),        // #0757B8
        AccentText: Color.FromArgb(7, 87, 184),         // #0757B8
        AccentSubtleBg: Color.FromArgb(230, 241, 254),  // #E6F1FE
        Success: Color.FromArgb(26, 127, 55),           // #1A7F37 (5.08:1 w/ white)
        SuccessHover: Color.FromArgb(21, 105, 45),      // #15692D
        SuccessText: Color.FromArgb(20, 108, 46),       // #146C2E
        SuccessSubtleBg: Color.FromArgb(230, 246, 236), // #E6F6EC
        Warning: Color.FromArgb(154, 103, 0),           // #9A6700 (4.86:1 w/ white)
        WarningText: Color.FromArgb(138, 83, 0),        // #8A5300
        WarningSubtleBg: Color.FromArgb(255, 245, 219), // #FFF5DB
        Danger: Color.FromArgb(201, 32, 44),            // #C9202C (5.64:1 w/ white)
        DangerHover: Color.FromArgb(176, 25, 36),       // #B01924
        DangerText: Color.FromArgb(180, 27, 38),        // #B41B26
        DangerSubtleBg: Color.FromArgb(254, 236, 235),  // #FEECEB
        TextPrimary: Color.FromArgb(23, 28, 36),        // #171C24
        TextSecondary: Color.FromArgb(82, 94, 109),     // #525E6D
        TextDisabled: Color.FromArgb(138, 150, 165),    // #8A96A5
        InputBg: Color.FromArgb(255, 255, 255),         // #FFFFFF
        ButtonBg: Color.FromArgb(235, 238, 243),        // #EBEEF3
        ButtonHoverBg: Color.FromArgb(220, 226, 234),   // #DCE2EA
        FocusRing: Color.FromArgb(9, 105, 218));        // #0969DA

    public static ThemePalette GetPalette(bool useDarkTheme) => useDarkTheme ? Dark : Light;

    public static void ApplyTitleBarTheme(Form form, bool useDarkTheme)
    {
        if (!form.IsHandleCreated)
        {
            return;
        }

        var enabled = useDarkTheme ? 1 : 0;
        // DWMWA_USE_IMMERSIVE_DARK_MODE: 20 on Win10 2004+/Win11, 19 on earlier Win10 builds.
        _ = DwmSetWindowAttribute(form.Handle, 20, ref enabled, sizeof(int));
        _ = DwmSetWindowAttribute(form.Handle, 19, ref enabled, sizeof(int));
    }

    public static void StyleButton(Button button, ThemePalette palette, bool isPrimary = false)
    {
        if (button is Controls.ActionButton actionBtn)
        {
            actionBtn.Variant = isPrimary ? Controls.ActionButtonVariant.PrimaryAccent : Controls.ActionButtonVariant.Secondary;
            actionBtn.ApplyTheme(palette);
            return;
        }

        button.FlatStyle = FlatStyle.Flat;
        button.Cursor = Cursors.Hand;
        if (isPrimary)
        {
            button.BackColor = palette.Accent;
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = palette.AccentHover;
        }
        else
        {
            button.BackColor = palette.ButtonBg;
            button.ForeColor = palette.TextPrimary;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = palette.BorderStrong;
            button.FlatAppearance.MouseOverBackColor = palette.ButtonHoverBg;
        }
    }

    public static void StyleContextMenu(ContextMenuStrip menu, ThemePalette palette)
    {
        menu.BackColor = palette.SurfaceBg;
        menu.ForeColor = palette.TextPrimary;
        menu.ShowImageMargin = false;
        menu.Renderer = new ThemedMenuRenderer(palette);
        foreach (ToolStripItem item in menu.Items)
        {
            item.ForeColor = item.Enabled ? palette.TextPrimary : palette.TextDisabled;
            item.BackColor = palette.SurfaceBg;
        }
    }

    internal static Color ResolveOpaqueBackground(Control? control, Color fallback)
    {
        var current = control;
        while (current is not null)
        {
            if (current.BackColor.A == byte.MaxValue && current.BackColor != Color.Transparent)
            {
                return current.BackColor;
            }
            current = current.Parent;
        }
        return fallback;
    }

    internal static GraphicsPath CreateRoundedRectPath(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return path;
        }

        var maxRadius = Math.Min(bounds.Width, bounds.Height) / 2f;
        var r = Math.Clamp(radius, 0f, maxRadius);
        if (r <= 0.5f)
        {
            path.AddRectangle(bounds);
            return path;
        }

        var d = r * 2f;
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    internal static double GetContrastRatio(Color foreground, Color background)
    {
        var l1 = GetRelativeLuminance(foreground);
        var l2 = GetRelativeLuminance(background);
        var lighter = Math.Max(l1, l2);
        var darker = Math.Min(l1, l2);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double GetRelativeLuminance(Color c)
    {
        static double Linearize(byte channel)
        {
            var srgb = channel / 255.0;
            return srgb <= 0.03928
                ? srgb / 12.92
                : Math.Pow((srgb + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Linearize(c.R) + 0.7152 * Linearize(c.G) + 0.0722 * Linearize(c.B);
    }

    private sealed class ThemedMenuRenderer : ToolStripProfessionalRenderer
    {
        private readonly ThemePalette _palette;

        public ThemedMenuRenderer(ThemePalette palette)
            : base(new ThemedColorTable(palette))
        {
            _palette = palette;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? _palette.TextPrimary : _palette.TextDisabled;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            var y = e.Item.Height / 2;
            using var pen = new Pen(_palette.Border);
            e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
        }
    }

    private sealed class ThemedColorTable : ProfessionalColorTable
    {
        private readonly ThemePalette _palette;

        public ThemedColorTable(ThemePalette palette)
        {
            _palette = palette;
            UseSystemColors = false;
        }

        public override Color ToolStripDropDownBackground => _palette.SurfaceBg;
        public override Color MenuBorder => _palette.BorderStrong;
        public override Color MenuItemBorder => _palette.Accent;
        public override Color MenuItemSelected => _palette.CardHoverBg;
        public override Color MenuItemSelectedGradientBegin => _palette.CardHoverBg;
        public override Color MenuItemSelectedGradientEnd => _palette.CardHoverBg;
        public override Color ImageMarginGradientBegin => _palette.SurfaceBg;
        public override Color ImageMarginGradientMiddle => _palette.SurfaceBg;
        public override Color ImageMarginGradientEnd => _palette.SurfaceBg;
        public override Color SeparatorDark => _palette.Border;
        public override Color SeparatorLight => _palette.Border;
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
}
