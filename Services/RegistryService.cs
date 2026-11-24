using System.Diagnostics;
using System.Security.Principal;
using Grand_Theft_Auto_V_Platform_Switcher.Models;
using Microsoft.Win32;

namespace Grand_Theft_Auto_V_Platform_Switcher.Services;

public sealed record RegistryStatus(
    bool SteamInstalledFlag,
    string? InstallFolderSteam,
    string? InstallFolderEpic,
    string? InstallFolderRgl);

public sealed class RegistryService
{
    private readonly AppLogger _logger;

    public RegistryService(AppLogger logger)
    {
        _logger = logger;
    }

    public bool IsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    public bool RestartAsAdministrator(string? arguments = null)
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments ?? string.Empty,
                UseShellExecute = true,
                Verb = "runas"
            });
            return true;
        }
        catch (Exception ex)
        {
            _logger.Warn($"Elevation cancelled or failed: {ex.Message}");
            return false;
        }
    }

    public RegistryStatus ReadStatus(EditionSpec edition)
    {
        var steamInstalled = false;
        try
        {
            using var steamAppKey = Registry.CurrentUser.OpenSubKey(
                $@"SOFTWARE\Valve\Steam\Apps\{edition.SteamAppId}",
                writable: false);
            if (steamAppKey?.GetValue("Installed") is int val && val == 1)
            {
                steamInstalled = true;
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed reading Steam registry status: {ex.Message}");
        }

        string? steamFolder = null;
        string? epicFolder = null;
        string? rglFolder = null;

        try
        {
            using var hklm32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            using var steamKey = hklm32.OpenSubKey(edition.SteamRegistrySubKey, writable: false);
            steamFolder = steamKey?.GetValue("InstallFolderSteam") as string;

            using var rglKey = hklm32.OpenSubKey(edition.EpicAndRglRegistrySubKey, writable: false);
            epicFolder = rglKey?.GetValue("InstallFolderEpic") as string;
            rglFolder = rglKey?.GetValue("InstallFolder") as string;
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed reading Rockstar registry keys: {ex.Message}");
        }

        return new RegistryStatus(steamInstalled, steamFolder, epicFolder, rglFolder);
    }

    public void AddPlayOnSteam(EditionSpec edition, string gameDirectory)
    {
        EnsureElevated();
        var cleanPath = NormalizePath(gameDirectory);

        using (var steamAppKey = Registry.CurrentUser.CreateSubKey($@"SOFTWARE\Valve\Steam\Apps\{edition.SteamAppId}"))
        {
            steamAppKey.SetValue("Installed", 1, RegistryValueKind.DWord);
        }

        using (var hklm32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
        using (var rglSteamKey = hklm32.CreateSubKey(edition.SteamRegistrySubKey))
        {
            rglSteamKey.SetValue("InstallFolderSteam", cleanPath, RegistryValueKind.String);
        }

        _logger.Info($"Added Steam RGL registry entries for {edition.DisplayName} -> {cleanPath}");
    }

    public void RemovePlayOnSteam(EditionSpec edition)
    {
        EnsureElevated();

        using (var steamAppKey = Registry.CurrentUser.CreateSubKey($@"SOFTWARE\Valve\Steam\Apps\{edition.SteamAppId}"))
        {
            steamAppKey.SetValue("Installed", 0, RegistryValueKind.DWord);
        }

        using (var hklm32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
        using (var rglSteamKey = hklm32.OpenSubKey(edition.SteamRegistrySubKey, writable: true))
        {
            rglSteamKey?.DeleteValue("InstallFolderSteam", throwOnMissingValue: false);
        }

        _logger.Info($"Removed Steam RGL registry entries for {edition.DisplayName}");
    }

    public void AddPlayOnEpic(EditionSpec edition, string gameDirectory)
    {
        EnsureElevated();
        var cleanPath = NormalizePath(gameDirectory);

        using var hklm32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
        using var rglKey = hklm32.CreateSubKey(edition.EpicAndRglRegistrySubKey);
        rglKey.SetValue("InstallFolderEpic", cleanPath, RegistryValueKind.String);
        if (rglKey.GetValue("InstallFolder") is null)
        {
            rglKey.SetValue("InstallFolder", cleanPath, RegistryValueKind.String);
        }

        _logger.Info($"Added Epic RGL registry entries for {edition.DisplayName} -> {cleanPath}");
    }

    public void RemovePlayOnEpic(EditionSpec edition)
    {
        EnsureElevated();

        using var hklm32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
        using var rglKey = hklm32.OpenSubKey(edition.EpicAndRglRegistrySubKey, writable: true);
        rglKey?.DeleteValue("InstallFolderEpic", throwOnMissingValue: false);

        _logger.Info($"Removed Epic RGL registry entries for {edition.DisplayName}");
    }

    public void SyncPlayOnRgl(EditionSpec edition, string gameDirectory)
    {
        EnsureElevated();
        var cleanPath = NormalizePath(gameDirectory);

        using (var hklm32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
        using (var rglKey = hklm32.CreateSubKey(edition.EpicAndRglRegistrySubKey))
        {
            rglKey.SetValue("InstallFolder", cleanPath, RegistryValueKind.String);
        }

        RemovePlayOnSteam(edition);
        _logger.Info($"Synchronized RGL registry entry for {edition.DisplayName} -> {cleanPath}");
    }

    private void EnsureElevated()
    {
        if (!IsAdministrator())
        {
            throw new UnauthorizedAccessException("Administrator privileges are required to modify Windows Registry keys.");
        }
    }

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path.Trim().Trim('"')).TrimEnd('\\', '/');
}
