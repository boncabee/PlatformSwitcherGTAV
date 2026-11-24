using System.Text.Json.Serialization;

namespace Grand_Theft_Auto_V_Platform_Switcher.Models;

public sealed class EditionSettings
{
    public string GameDirectory { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public GamePlatform CurrentPlatform { get; set; } = GamePlatform.Unknown;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public GamePlatform PreviousPlatform { get; set; } = GamePlatform.Unknown;

    public string EpicAppIdOverride { get; set; } = string.Empty;

    public bool FirstRunDoctorShown { get; set; }
}

public sealed class AppSettings
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public GameEdition SelectedEdition { get; set; } = GameEdition.Legacy;

    public EditionSettings Legacy { get; set; } = new();
    public EditionSettings Enhanced { get; set; } = new();

    public bool DisableBattlEyeOnLaunch { get; set; }
    public bool UseDarkTheme { get; set; } = true;
    public bool CheckUpdatesOnStartup { get; set; } = true;
    public bool FirstRunDoctorShown { get; set; }

    public EditionSettings GetEditionSettings(GameEdition edition) => edition switch
    {
        GameEdition.Enhanced => Enhanced,
        _ => Legacy
    };
}
