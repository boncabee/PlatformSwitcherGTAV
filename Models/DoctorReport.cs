namespace Grand_Theft_Auto_V_Platform_Switcher.Models;

public enum DoctorSeverity
{
    Ok,
    Warning,
    Error
}

public enum DoctorFixType
{
    None,
    AutoDetectGamePath,
    BrowseGamePath,
    RestartAsAdmin,
    BackupActivePlatform,
    RepairSteamApiConflict,
    RepairEpicDllConflict,
    SyncRegistryForActivePlatform,
    CleanInvalidProfile,
    AdoptEpicInstallation
}

public sealed class DoctorCheckItem
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required DoctorSeverity Severity { get; init; }
    public required string Summary { get; init; }
    public string Details { get; init; } = string.Empty;
    public DoctorFixType FixType { get; init; } = DoctorFixType.None;
    public string FixLabel { get; init; } = string.Empty;
    public GamePlatform SuggestedPlatform { get; init; } = GamePlatform.Unknown;
}

public sealed class DoctorReport
{
    public required GameEdition Edition { get; init; }
    public required string GameDirectory { get; init; }
    public required GamePlatform ConfiguredPlatform { get; init; }
    public required GamePlatform DetectedActivePlatform { get; init; }
    public required bool IsElevated { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
    public required IReadOnlyList<DoctorCheckItem> Checks { get; init; }

    public int ErrorCount => Checks.Count(c => c.Severity == DoctorSeverity.Error);
    public int WarningCount => Checks.Count(c => c.Severity == DoctorSeverity.Warning);
    public bool HasIssues => ErrorCount > 0 || WarningCount > 0;
}
