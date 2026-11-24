using Grand_Theft_Auto_V_Platform_Switcher.Models;

namespace Grand_Theft_Auto_V_Platform_Switcher.Services;

public sealed record CliOptions(
    bool HasCliCommand,
    bool ShowHelp,
    GameEdition? Edition,
    GamePlatform? SwitchTo,
    bool Launch,
    bool? DisableBattlEye,
    string? Error);

public sealed class CliRunner
{
    private readonly SettingsService _settingsService;
    private readonly EditionDetector _detector;
    private readonly PlatformSwitchService _switchService;
    private readonly GameLauncherService _launcherService;
    private readonly AppLogger _logger;

    public CliRunner(
        SettingsService settingsService,
        EditionDetector detector,
        PlatformSwitchService switchService,
        GameLauncherService launcherService,
        AppLogger logger)
    {
        _settingsService = settingsService;
        _detector = detector;
        _switchService = switchService;
        _launcherService = launcherService;
        _logger = logger;
    }

    public static CliOptions ParseArguments(string[] args)
    {
        if (args.Length == 0)
        {
            return new CliOptions(false, false, null, null, false, null, null);
        }

        GameEdition? edition = null;
        GamePlatform? switchTo = null;
        var launch = false;
        bool? disableBe = null;
        var showHelp = false;

        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i].Trim().ToLowerInvariant();
            switch (token)
            {
                case "--help":
                case "-h":
                case "/?":
                    showHelp = true;
                    break;

                case "--edition":
                case "-e":
                    if (i + 1 >= args.Length || !EditionSpec.TryParseEdition(args[++i], out var parsedEdition))
                    {
                        return new CliOptions(true, false, null, null, false, null,
                            "Invalid --edition value. Use 'legacy' or 'enhanced'.");
                    }
                    edition = parsedEdition;
                    break;

                case "--switch":
                case "-s":
                    if (i + 1 >= args.Length || !EditionSpec.TryParsePlatform(args[++i], out var parsedPlatform))
                    {
                        return new CliOptions(true, false, null, null, false, null,
                            "Invalid --switch value. Use 'rgl', 'steam', or 'egl'.");
                    }
                    switchTo = parsedPlatform;
                    break;

                case "--launch":
                case "-l":
                    launch = true;
                    break;

                case "--nobattleye":
                    disableBe = true;
                    break;

                case "--battleye":
                    disableBe = false;
                    break;

                default:
                    return new CliOptions(true, false, null, null, false, null,
                        $"Unknown argument: '{args[i]}'. Use --help to view usage.");
            }
        }

        var hasCommand = showHelp || switchTo.HasValue || launch || edition.HasValue || disableBe.HasValue;
        return new CliOptions(hasCommand, showHelp, edition, switchTo, launch, disableBe, null);
    }

    public static string GetHelpText() =>
        """
        Grand Theft Auto V Platform Switcher v2.0 - CLI Usage:

          --edition <legacy|enhanced>   Select GTA V edition (defaults to saved edition)
          --switch <rgl|steam|egl>      Switch platform files before launching
          --launch                      Launch the game after switching
          --nobattleye                  Pass -nobattleye for Story Mode / single-player
          --battleye                    Force BattlEye enabled for this launch
          --help                        Show this usage guide
        """;

    public async Task<int> ExecuteAsync(CliOptions options)
    {
        if (options.ShowHelp)
        {
            MessageBox.Show(GetHelpText(), "GTA V Platform Switcher - CLI Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }

        if (!string.IsNullOrWhiteSpace(options.Error))
        {
            _logger.Error($"CLI error: {options.Error}");
            MessageBox.Show(options.Error, "CLI Argument Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }

        var settings = _settingsService.Load();
        var editionEnum = options.Edition ?? settings.SelectedEdition;
        var edition = EditionSpec.FromEdition(editionEnum);
        var editionSettings = settings.GetEditionSettings(editionEnum);

        if (!_detector.IsValidGameDirectory(edition, editionSettings.GameDirectory))
        {
            var detected = _detector.DetectInstallation(edition);
            if (detected.GameDirectory is not null)
            {
                editionSettings.GameDirectory = detected.GameDirectory;
            }
            else
            {
                var msg = $"CLI: Could not locate {edition.DisplayName} installation directory.";
                _logger.Error(msg);
                MessageBox.Show(msg, "GTA V Platform Switcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 2;
            }
        }

        if (options.SwitchTo.HasValue)
        {
            var target = options.SwitchTo.Value;
            var result = await _switchService.SwitchAsync(edition, editionSettings.GameDirectory, target).ConfigureAwait(false);
            if (!result.Success)
            {
                _logger.Error($"CLI switch failed: {result.Message}");
                MessageBox.Show(result.Message, "Switch Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 3;
            }

            editionSettings.PreviousPlatform = editionSettings.CurrentPlatform;
            editionSettings.CurrentPlatform = target;
            settings.SelectedEdition = editionEnum;
            _settingsService.Save(settings);
        }

        if (options.Launch)
        {
            var platform = options.SwitchTo ?? editionSettings.CurrentPlatform;
            var disableBe = options.DisableBattlEye ?? settings.DisableBattlEyeOnLaunch;
            var launchRes = _launcherService.Launch(
                edition,
                editionSettings.GameDirectory,
                platform,
                disableBe,
                editionSettings.EpicAppIdOverride);

            if (!launchRes.Success)
            {
                _logger.Error($"CLI launch failed: {launchRes.Message}");
                MessageBox.Show(launchRes.Message, "Launch Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 4;
            }
        }

        return 0;
    }
}
