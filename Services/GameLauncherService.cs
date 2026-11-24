using System.Diagnostics;
using Grand_Theft_Auto_V_Platform_Switcher.Models;

namespace Grand_Theft_Auto_V_Platform_Switcher.Services;

public sealed record LaunchPlan(
    bool IsValid,
    string FileName,
    string Arguments,
    string WorkingDirectory,
    string? Notice = null,
    string? Error = null);

public sealed class GameLauncherService
{
    private readonly EditionDetector _detector;
    private readonly AppLogger _logger;

    public GameLauncherService(EditionDetector detector, AppLogger logger)
    {
        _detector = detector;
        _logger = logger;
    }

    public LaunchPlan BuildLaunchPlan(
        EditionSpec edition,
        string? gameDirectory,
        GamePlatform platform,
        bool disableBattlEye,
        string? epicAppIdOverride = null)
    {
        if (platform == GamePlatform.Unknown)
        {
            return new LaunchPlan(false, string.Empty, string.Empty, string.Empty,
                Error: "Current launcher is unknown. Switch to Rockstar Games, Steam, or Epic Games first.");
        }

        switch (platform)
        {
            case GamePlatform.Steam:
            {
                var uri = disableBattlEye
                    ? $"steam://rungameid/{edition.SteamAppId}//-nobattleye"
                    : $"steam://rungameid/{edition.SteamAppId}";
                var notice = disableBattlEye
                    ? "Steam may show a confirmation dialog when disabling BattlEye. GTA Online requires BattlEye to be enabled."
                    : null;
                return new LaunchPlan(true, uri, string.Empty, gameDirectory ?? string.Empty, Notice: notice);
            }

            case GamePlatform.RGL:
            {
                if (string.IsNullOrWhiteSpace(gameDirectory) || !Directory.Exists(gameDirectory))
                {
                    return new LaunchPlan(false, string.Empty, string.Empty, string.Empty,
                        Error: "Game directory is not configured.");
                }

                var playExe = Path.Combine(gameDirectory, "PlayGTAV.exe");
                var targetExe = File.Exists(playExe)
                    ? playExe
                    : Path.Combine(gameDirectory, edition.MainExecutable);

                if (!File.Exists(targetExe))
                {
                    return new LaunchPlan(false, string.Empty, string.Empty, string.Empty,
                        Error: $"Could not find PlayGTAV.exe or {edition.MainExecutable} in '{gameDirectory}'.");
                }

                var args = disableBattlEye ? "-nobattleye" : string.Empty;
                var notice = disableBattlEye
                    ? "Note: Rockstar Games Launcher's own 'Enable BattlEye' setting can keep BattlEye enabled unless turned off in Rockstar Games Launcher Settings."
                    : null;
                return new LaunchPlan(true, targetExe, args, gameDirectory, Notice: notice);
            }

            case GamePlatform.EGL:
            {
                var epicAppId = !string.IsNullOrWhiteSpace(epicAppIdOverride)
                    ? epicAppIdOverride.Trim()
                    : _detector.FindEpicAppName(edition, gameDirectory);

                if (string.IsNullOrWhiteSpace(epicAppId))
                {
                    return new LaunchPlan(false, string.Empty, string.Empty, string.Empty,
                        Error: $"Epic Games App ID for {edition.DisplayName} could not be auto-detected. Set it manually in Settings.");
                }

                var uri = $"com.epicgames.launcher://apps/{epicAppId}?action=launch&silent=true";
                var notice = disableBattlEye
                    ? "Epic Games cannot turn off BattlEye automatically when started from here. To disable BattlEye for Story Mode, turn off 'Enable BattlEye' in Rockstar Games Launcher Settings."
                    : null;
                return new LaunchPlan(true, uri, string.Empty, gameDirectory ?? string.Empty, Notice: notice);
            }

            default:
                return new LaunchPlan(false, string.Empty, string.Empty, string.Empty, Error: "Unsupported platform.");
        }
    }

    public OperationResult Launch(
        EditionSpec edition,
        string? gameDirectory,
        GamePlatform platform,
        bool disableBattlEye,
        string? epicAppIdOverride = null)
    {
        var plan = BuildLaunchPlan(edition, gameDirectory, platform, disableBattlEye, epicAppIdOverride);
        if (!plan.IsValid)
        {
            return new OperationResult(false, plan.Error ?? "Unable to build launch command.");
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = plan.FileName,
                Arguments = plan.Arguments,
                UseShellExecute = true
            };

            if (!string.IsNullOrWhiteSpace(plan.WorkingDirectory) && Directory.Exists(plan.WorkingDirectory))
            {
                psi.WorkingDirectory = plan.WorkingDirectory;
            }

            Process.Start(psi);
            _logger.Info($"Launched {edition.DisplayName} ({platform}): {plan.FileName} {plan.Arguments}".Trim());
            var message = string.IsNullOrWhiteSpace(plan.Notice)
                ? $"Launching {edition.DisplayName} via {EditionSpec.GetPlatformDisplayName(platform)}..."
                : $"Launching {edition.DisplayName} via {EditionSpec.GetPlatformDisplayName(platform)}.\n\n{plan.Notice}";
            return new OperationResult(true, message);
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to launch {edition.DisplayName} ({platform}).", ex);
            return new OperationResult(false, $"Failed to launch game: {ex.Message}");
        }
    }

    public OperationResult CreateDesktopShortcut(
        EditionSpec edition,
        GamePlatform platform,
        bool disableBattlEye) =>
        CreateDesktopShortcutAsync(edition, platform, disableBattlEye).GetAwaiter().GetResult();

    public async Task<OperationResult> CreateDesktopShortcutAsync(
        EditionSpec edition,
        GamePlatform platform,
        bool disableBattlEye,
        CancellationToken cancellationToken = default)
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            return new OperationResult(false, "Cannot determine executable path for shortcut creation.");
        }

        try
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var suffix = disableBattlEye ? " (No BattlEye)" : string.Empty;
            var friendlyPlatform = platform switch
            {
                GamePlatform.RGL => "Rockstar Games",
                GamePlatform.Steam => "Steam",
                GamePlatform.EGL => "Epic Games",
                _ => platform.ToString()
            };
            var shortcutName = $"{edition.DisplayName} - {friendlyPlatform}{suffix}.lnk";
            var shortcutPath = Path.Combine(desktop, shortcutName);

            var editionArg = edition.Edition == GameEdition.Enhanced ? "enhanced" : "legacy";
            var platformArg = platform.ToString().ToLowerInvariant();
            var args = $"--edition {editionArg} --switch {platformArg} --launch" +
                       (disableBattlEye ? " --nobattleye" : string.Empty);

            var escapedShortcut = EscapePs(shortcutPath);
            var escapedTarget = EscapePs(exePath);
            var escapedArgs = EscapePs(args);
            var escapedWorkDir = EscapePs(Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory);

            var script =
                "$ws = New-Object -ComObject WScript.Shell; " +
                $"$s = $ws.CreateShortcut('{escapedShortcut}'); " +
                $"$s.TargetPath = '{escapedTarget}'; " +
                $"$s.Arguments = '{escapedArgs}'; " +
                $"$s.WorkingDirectory = '{escapedWorkDir}'; " +
                $"$s.IconLocation = '{escapedTarget},0'; " +
                "$s.Save();";

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -WindowStyle Hidden -Command \"{script}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            if (proc is null)
            {
                return new OperationResult(false, "Failed to invoke shortcut helper.");
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

            try
            {
                var errTask = proc.StandardError.ReadToEndAsync(timeoutCts.Token);
                await proc.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
                var err = await errTask.ConfigureAwait(false);

                if (proc.ExitCode != 0 || !File.Exists(shortcutPath))
                {
                    return new OperationResult(false, $"Shortcut creation failed: {err}");
                }
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!proc.HasExited)
                    {
                        proc.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                    // Ignore process termination errors on timeout/cancellation.
                }

                return cancellationToken.IsCancellationRequested
                    ? new OperationResult(false, "Shortcut creation was cancelled.")
                    : new OperationResult(false, "Shortcut creation timed out after 5 seconds.");
            }

            _logger.Info($"Created desktop shortcut: {shortcutPath}");
            return new OperationResult(true, $"Desktop shortcut created:\n{shortcutPath}");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to create desktop shortcut.", ex);
            return new OperationResult(false, $"Failed to create desktop shortcut: {ex.Message}");
        }
    }

    private static string EscapePs(string value) => value.Replace("'", "''");
}
