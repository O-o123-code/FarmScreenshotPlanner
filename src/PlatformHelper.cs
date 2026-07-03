using System.Diagnostics;

namespace FarmScreenshotPlanner;

public static class PlatformHelper
{
    public static bool TryRevealFileInExplorer(string filePath)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo("explorer", $"/select,\"{filePath}\"") { UseShellExecute = true });
                return true;
            }
            else if (OperatingSystem.IsLinux())
            {
                string dir = Path.GetDirectoryName(filePath)!;
                Process.Start(new ProcessStartInfo("xdg-open", $"\"{dir}\"") { UseShellExecute = true });
                return true;
            }
            else if (OperatingSystem.IsMacOS())
            {
                string dir = Path.GetDirectoryName(filePath)!;
                Process.Start(new ProcessStartInfo("open", $"\"{dir}\"") { UseShellExecute = true });
                return true;
            }
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
