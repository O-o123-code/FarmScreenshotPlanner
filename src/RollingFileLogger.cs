using StardewModdingAPI;

namespace FarmScreenshotPlanner;

public class RollingFileLogger : IDisposable
{
    private readonly string _logDir;
    private readonly string _logPath;
    private readonly long _maxBytes = 1 * 1024 * 1024;
    private const int MaxFiles = 3;
    private readonly object _logLock = new();
    private StreamWriter? _writer;
    private bool _disposed;

    public RollingFileLogger(string modDirectory)
    {
        _logDir = Path.Combine(modDirectory, "logs");
        _logPath = Path.Combine(_logDir, "FarmScreenshotPlanner.log");
        Directory.CreateDirectory(_logDir);
        _writer = new StreamWriter(_logPath, append: true) { AutoFlush = true };
    }

    public void Debug(string message) => Write("DEBUG", message);
    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        lock (_logLock)
        {
            if (_disposed) return;
            CheckRollover();
            _writer!.WriteLine($"[{level}] {DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}");
        }
    }

    private void CheckRollover()
    {
        if (_writer is null) return;
        _writer.Flush();
        var fi = new FileInfo(_logPath);
        if (!fi.Exists || fi.Length < _maxBytes) return;

        _writer.Close();
        _writer = null;

        for (int i = MaxFiles - 1; i >= 1; i--)
        {
            string src = $"{_logPath}.{i}";
            string dst = $"{_logPath}.{i + 1}";
            if (File.Exists(dst)) File.Delete(dst);
            if (File.Exists(src)) File.Move(src, dst);
        }

        if (File.Exists(_logPath))
        {
            string first = $"{_logPath}.1";
            if (File.Exists(first)) File.Delete(first);
            File.Move(_logPath, first);
        }

        _writer = new StreamWriter(_logPath, append: false) { AutoFlush = true };
    }

    public void Dispose()
    {
        lock (_logLock)
        {
            if (_disposed) return;
            _disposed = true;
            _writer?.Close();
            _writer?.Dispose();
            _writer = null;
        }
    }
}
