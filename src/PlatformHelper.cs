using System.Diagnostics;

namespace FarmScreenshotPlanner;

public static class PlatformHelper
{
    public static void RevealFileInExplorer(string filePath)
    {
        if (OperatingSystem.IsWindows())
        {
            Process.Start("explorer", $"/select,\"{filePath}\"");
        }
        else if (OperatingSystem.IsLinux())
        {
            string dir = Path.GetDirectoryName(filePath)!;
            Process.Start("xdg-open", $"\"{dir}\"");
        }
        else if (OperatingSystem.IsMacOS())
        {
            string dir = Path.GetDirectoryName(filePath)!;
            Process.Start("open", $"\"{dir}\"");
        }
    }
}
