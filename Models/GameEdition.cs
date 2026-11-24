namespace Grand_Theft_Auto_V_Platform_Switcher.Models;

public enum GameEdition
{
    Legacy,
    Enhanced
}

public enum GamePlatform
{
    Unknown,
    RGL,
    Steam,
    EGL
}

public sealed class EditionSpec
{
    public required GameEdition Edition { get; init; }
    public required string DisplayName { get; init; }
    public required string ProfileFolderName { get; init; }
    public required string MainExecutable { get; init; }
    public required string BattlEyeExecutable { get; init; }
    public required string SteamAppId { get; init; }
    public required string DefaultEpicAppId { get; init; }
    public required string SteamRegistrySubKey { get; init; }
    public required string EpicAndRglRegistrySubKey { get; init; }
    public required string[] TrackedPlatformFiles { get; init; }
    public required string[] BlockingProcessNames { get; init; }

    public static readonly EditionSpec Legacy = new()
    {
        Edition = GameEdition.Legacy,
        DisplayName = "GTA V Legacy",
        ProfileFolderName = "Legacy",
        MainExecutable = "GTA5.exe",
        BattlEyeExecutable = "GTA5_BE.exe",
        SteamAppId = "271590",
        DefaultEpicAppId = "9d2d0eb64d5c44529cece33fe2a46482",
        SteamRegistrySubKey = @"SOFTWARE\Rockstar Games\GTAV",
        EpicAndRglRegistrySubKey = @"SOFTWARE\Rockstar Games\Grand Theft Auto V",
        TrackedPlatformFiles =
        [
            "GTA5.exe",
            "GTA5_BE.exe",
            "PlayGTAV.exe",
            "GTAVLauncher.exe",
            "GTAVLanguageSelect.exe",
            "title.rgl",
            "EOSSDK-Win64-Shipping.dll"
        ],
        BlockingProcessNames =
        [
            "GTA5",
            "GTA5_BE",
            "PlayGTAV",
            "GTAVLauncher"
        ]
    };

    public static readonly EditionSpec Enhanced = new()
    {
        Edition = GameEdition.Enhanced,
        DisplayName = "GTA V Enhanced",
        ProfileFolderName = "Enhanced",
        MainExecutable = "GTA5_Enhanced.exe",
        BattlEyeExecutable = "GTA5_Enhanced_BE.exe",
        SteamAppId = "3240220",
        DefaultEpicAppId = string.Empty,
        SteamRegistrySubKey = @"SOFTWARE\Rockstar Games\GTA V Enhanced",
        EpicAndRglRegistrySubKey = @"SOFTWARE\Rockstar Games\GTA V Enhanced",
        TrackedPlatformFiles =
        [
            "GTA5_Enhanced.exe",
            "GTA5_Enhanced_BE.exe",
            "PlayGTAV.exe",
            "title.rgl",
            "EOSSDK-Win64-Shipping.dll"
        ],
        BlockingProcessNames =
        [
            "GTA5_Enhanced",
            "GTA5_Enhanced_BE",
            "PlayGTAV"
        ]
    };

    public static EditionSpec FromEdition(GameEdition edition) => edition switch
    {
        GameEdition.Legacy => Legacy,
        GameEdition.Enhanced => Enhanced,
        _ => Legacy
    };

    public static string GetPlatformDisplayName(GamePlatform platform) => platform switch
    {
        GamePlatform.RGL => "Rockstar Games Launcher",
        GamePlatform.Steam => "Steam",
        GamePlatform.EGL => "Epic Games Launcher",
        _ => "Unknown"
    };

    public static string GetPlatformFolderName(GamePlatform platform) => platform switch
    {
        GamePlatform.RGL => "RGL",
        GamePlatform.Steam => "Steam",
        GamePlatform.EGL => "EGL",
        _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, "Unsupported platform.")
    };

    public static bool TryParsePlatform(string? value, out GamePlatform platform)
    {
        platform = GamePlatform.Unknown;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "rgl":
            case "rockstar":
                platform = GamePlatform.RGL;
                return true;
            case "steam":
                platform = GamePlatform.Steam;
                return true;
            case "egl":
            case "epic":
                platform = GamePlatform.EGL;
                return true;
            default:
                return false;
        }
    }

    public static bool TryParseEdition(string? value, out GameEdition edition)
    {
        edition = GameEdition.Legacy;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "legacy":
                edition = GameEdition.Legacy;
                return true;
            case "enhanced":
                edition = GameEdition.Enhanced;
                return true;
            default:
                return false;
        }
    }
}
