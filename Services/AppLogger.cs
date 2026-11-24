namespace Grand_Theft_Auto_V_Platform_Switcher.Services;

public sealed class AppLogger
{
    private const long MaxLogBytes = 2 * 1024 * 1024;
    private readonly object _sync = new();

    public string LogDirectory { get; }
    public string LogFilePath { get; }

    public AppLogger(string? logDirectory = null)
    {
        LogDirectory = string.IsNullOrWhiteSpace(logDirectory)
            ? Path.Combine(
                AppContext.BaseDirectory,
                "Data",
                "Logs")
            : logDirectory;
        LogFilePath = Path.Combine(LogDirectory, "switcher.log");
    }

    public void Info(string message) => Write("INFO", message, null);
    public void Warn(string message) => Write("WARN", message, null);
    public void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private void Write(string level, string message, Exception? ex)
    {
        try
        {
            lock (_sync)
            {
                Directory.CreateDirectory(LogDirectory);
                RotateIfNeeded();

                using var writer = new StreamWriter(LogFilePath, append: true);
                writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}");

                var current = ex;
                while (current != null)
                {
                    writer.WriteLine($"  Exception: {current.GetType().FullName}: {current.Message}");
                    if (!string.IsNullOrWhiteSpace(current.StackTrace))
                    {
                        writer.WriteLine(current.StackTrace);
                    }
                    current = current.InnerException;
                }
            }
        }
        catch
        {
            // Logging must never crash the application.
        }
    }

    private void RotateIfNeeded()
    {
        if (!File.Exists(LogFilePath))
        {
            return;
        }

        var info = new FileInfo(LogFilePath);
        if (info.Length < MaxLogBytes)
        {
            return;
        }

        var archivePath = Path.Combine(LogDirectory, "switcher.prev.log");
        if (File.Exists(archivePath))
        {
            File.Delete(archivePath);
        }

        File.Move(LogFilePath, archivePath);
    }
}
