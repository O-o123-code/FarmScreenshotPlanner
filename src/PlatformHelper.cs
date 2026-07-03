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
                Process.Start("explorer", $"/select,\"{filePath}\"");
                return true;
            }
            else if (OperatingSystem.IsLinux())
            {
                string dir = Path.GetDirectoryName(filePath)!;
                Process.Start("xdg-open", $"\"{dir}\"");
                return true;
            }
            else if (OperatingSystem.IsMacOS())
            {
                string dir = Path.GetDirectoryName(filePath)!;
                Process.Start("open", $"\"{dir}\"");
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
