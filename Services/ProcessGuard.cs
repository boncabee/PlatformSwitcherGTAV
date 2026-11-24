using System.Diagnostics;
using Grand_Theft_Auto_V_Platform_Switcher.Models;

namespace Grand_Theft_Auto_V_Platform_Switcher.Services;

public sealed class ProcessGuard
{
    private readonly Func<string, bool>? _processRunningProbe;

    public ProcessGuard(Func<string, bool>? processRunningProbe = null)
    {
        _processRunningProbe = processRunningProbe;
    }

    public IReadOnlyList<string> GetBlockingProcesses(EditionSpec edition)
    {
        var blocking = new List<string>();
        foreach (var procName in edition.BlockingProcessNames)
        {
            if (IsProcessRunning(procName))
            {
                blocking.Add(procName + ".exe");
            }
        }

        return blocking;
    }

    public bool IsProcessRunning(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        var normalized = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName[..^4]
            : processName;

        if (_processRunningProbe is not null)
        {
            return _processRunningProbe(normalized);
        }

        try
        {
            var running = Process.GetProcessesByName(normalized);
            var isRunning = running.Length > 0;
            foreach (var p in running)
            {
                p.Dispose();
            }

            return isRunning;
        }
        catch
        {
            // Ignore access errors when enumerating unrelated processes.
            return false;
        }
    }

    public bool IsEpicGamesLauncherRunning() =>
        IsProcessRunning("EpicGamesLauncher") ||
        IsProcessRunning("EpicOnlineServicesInstallHelper");
}
