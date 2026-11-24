using System.Windows.Forms;
using Grand_Theft_Auto_V_Platform_Switcher.Services;

namespace Grand_Theft_Auto_V_Platform_Switcher;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var logger = new AppLogger();
        var settingsService = new SettingsService(logger);
        var detector = new EditionDetector(logger);
        var processGuard = new ProcessGuard();
        var switchService = new PlatformSwitchService(detector, processGuard, logger);
        var battlEyeService = new BattlEyeService(logger);
        var registryService = new RegistryService(logger);
        var launcherService = new GameLauncherService(detector, logger);
        var epicManifestService = new EpicManifestService(processGuard, logger);
        var doctorService = new DoctorService(detector, switchService, battlEyeService, registryService, processGuard, epicManifestService);
        var updateService = new UpdateService(logger);

        ApplicationConfiguration.Initialize();

        Application.ThreadException += (_, e) =>
        {
            logger.Error("Unhandled UI thread exception.", e.Exception);
            MessageBox.Show(
                $"An unexpected error occurred:\n{e.Exception.Message}\n\nDetails were logged to {logger.LogFilePath}",
                "Unexpected Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                logger.Error("Unhandled domain exception.", ex);
            }
        };

        var cliOptions = CliRunner.ParseArguments(args);
        if (cliOptions.HasCliCommand)
        {
            var runner = new CliRunner(settingsService, detector, switchService, launcherService, logger);
            return runner.ExecuteAsync(cliOptions).GetAwaiter().GetResult();
        }

        using var mainWindow = new MainWindow(
            logger,
            settingsService,
            detector,
            processGuard,
            switchService,
            battlEyeService,
            registryService,
            launcherService,
            doctorService,
            updateService,
            epicManifestService);

        Application.Run(mainWindow);
        return 0;
    }
}
