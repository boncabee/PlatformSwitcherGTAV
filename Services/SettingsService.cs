using System.Text.Json;
using Grand_Theft_Auto_V_Platform_Switcher.Models;

namespace Grand_Theft_Auto_V_Platform_Switcher.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly AppLogger _logger;

    public string SettingsDirectory { get; }
    public string SettingsFilePath { get; }

    public SettingsService(AppLogger logger, string? settingsDirectory = null)
    {
        _logger = logger;
        SettingsDirectory = string.IsNullOrWhiteSpace(settingsDirectory)
            ? Path.Combine(
                AppContext.BaseDirectory,
                "Data")
            : settingsDirectory;
        SettingsFilePath = Path.Combine(SettingsDirectory, "settings.json");

        if (string.IsNullOrWhiteSpace(settingsDirectory))
        {
            MigrateLegacySettings();
        }
    }

    private void MigrateLegacySettings()
    {
        try
        {
            var old = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PlatformSwitcherGTAV",
                "settings.json");
            if (!File.Exists(SettingsFilePath) && File.Exists(old))
            {
                Directory.CreateDirectory(SettingsDirectory);
                File.Copy(old, SettingsFilePath);
            }
        }
        catch
        {
            // Best-effort migration only.
        }
    }

    public AppSettings Load()
    {
        if (!File.Exists(SettingsFilePath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(SettingsFilePath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (loaded is null)
            {
                return new AppSettings();
            }

            loaded.Legacy ??= new EditionSettings();
            loaded.Enhanced ??= new EditionSettings();
            return loaded;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to load settings from {SettingsFilePath}. Backing up corrupted file.", ex);
            BackupCorruptedSettings();
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        var tempPath = SettingsFilePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, SettingsFilePath, overwrite: true);
    }

    private void BackupCorruptedSettings()
    {
        try
        {
            var backupPath = Path.Combine(
                SettingsDirectory,
                $"settings.corrupted.{DateTime.Now:yyyyMMddHHmmss}.json");
            File.Copy(SettingsFilePath, backupPath, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.Error("Unable to back up corrupted settings file.", ex);
        }
    }
}
