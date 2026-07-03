using StardewModdingAPI;

namespace FarmScreenshotPlanner;

/// <summary>
/// Extension methods on IMonitor to provide convenient log-level methods,
/// replacing the custom RollingFileLogger with SMAPI's standard logging.
/// </summary>
internal static class MonitorHelper
{
    public static void Debug(this IMonitor monitor, string message)
        => monitor.Log(message, LogLevel.Debug);

    public static void Info(this IMonitor monitor, string message)
        => monitor.Log(message, LogLevel.Info);

    public static void Warn(this IMonitor monitor, string message)
        => monitor.Log(message, LogLevel.Warn);

    public static void Error(this IMonitor monitor, string message)
        => monitor.Log(message, LogLevel.Error);
}
