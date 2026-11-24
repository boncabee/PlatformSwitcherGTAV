using System.Diagnostics;
using Grand_Theft_Auto_V_Platform_Switcher.Models;
using Microsoft.Win32;

namespace Grand_Theft_Auto_V_Platform_Switcher.Services;

public sealed record BattlEyeStatus(
    bool IsExecutablePresent,
    string ExecutableName,
    string ExecutableVersion,
    bool IsFolderPresent,
    bool HasCoreFiles,
    string ServiceState,
    bool IsHealthy,
    string Summary);

public sealed class BattlEyeService
{
    private readonly AppLogger _logger;

    public BattlEyeService(AppLogger logger)
    {
        _logger = logger;
    }

    public BattlEyeStatus Inspect(EditionSpec edition, string? gameDirectory)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || !Directory.Exists(gameDirectory))
        {
            return new BattlEyeStatus(
                false,
                edition.BattlEyeExecutable,
                "N/A",
                false,
                false,
                "Unknown",
                false,
                "Game folder not selected yet.");
        }

        var beExePath = Path.Combine(gameDirectory, edition.BattlEyeExecutable);
        var isExePresent = File.Exists(beExePath);
        var exeVersion = "Missing";
        if (isExePresent)
        {
            try
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(beExePath);
                exeVersion = string.IsNullOrWhiteSpace(versionInfo.FileVersion)
                    ? "Present"
                    : versionInfo.FileVersion.Trim();
            }
            catch
            {
                exeVersion = "Present";
            }
        }

        var beFolder = Path.Combine(gameDirectory, "BattlEye");
        var isFolderPresent = Directory.Exists(beFolder);
        var hasCoreFiles = isFolderPresent &&
            (File.Exists(Path.Combine(beFolder, "BEService_x64.exe")) ||
             File.Exists(Path.Combine(beFolder, "BEClient_x64.dll")) ||
             File.Exists(Path.Combine(beFolder, "Install_BattlEye.bat")));

        var serviceState = QueryBeServiceState();
        var isHealthy = isExePresent && isFolderPresent && hasCoreFiles;

        var summary = isHealthy
            ? $"Ready (v{exeVersion}, Service: {serviceState})"
            : !isExePresent
                ? $"Missing anti-cheat file ({edition.BattlEyeExecutable}) — verify game files in your launcher"
                : !isFolderPresent || !hasCoreFiles
                    ? "BattlEye folder is missing or incomplete — verify game files in your launcher"
                    : $"Partial (Service: {serviceState})";

        return new BattlEyeStatus(
            isExePresent,
            edition.BattlEyeExecutable,
            exeVersion,
            isFolderPresent,
            hasCoreFiles,
            serviceState,
            isHealthy,
            summary);
    }

    public string QueryBeServiceState()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Services\BEService",
                writable: false);
            if (key is null)
            {
                return "Not Installed";
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"Unable to read BEService registry key: {ex.Message}");
            return "Unknown";
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = "query BEService",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc is null)
            {
                return "Installed";
            }

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(2000);

            if (output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase))
            {
                return "Running";
            }

            if (output.Contains("STOPPED", StringComparison.OrdinalIgnoreCase))
            {
                return "Installed (Stopped)";
            }

            return "Installed";
        }
        catch
        {
            return "Installed";
        }
    }
}
