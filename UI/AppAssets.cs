using System.Drawing;
using System.Reflection;

namespace Grand_Theft_Auto_V_Platform_Switcher.UI;

internal static class AppAssets
{
    private static readonly Assembly CurrentAssembly = Assembly.GetExecutingAssembly();

    public static Image? LoadImage(string assetFileName)
    {
        var resourceName = $"PlatformSwitcher.Assets.{assetFileName}";
        using var stream = CurrentAssembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null;
        }

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;
        using var temp = Image.FromStream(ms);
        return new Bitmap(temp);
    }

    public static Icon? LoadAppIcon()
    {
        using var stream = CurrentAssembly.GetManifestResourceStream("PlatformSwitcher.Assets.Switcher.ico");
        return stream is null ? null : new Icon(stream);
    }
}
