using Microsoft.Xna.Framework.Graphics;

namespace FarmScreenshotPlanner;

public class ScreenshotSaver
{
    public string Save(RenderTarget2D target, string directory, string prefix)
    {
        string fileName = $"{SanitizeFileName(prefix)}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        string fullPath = Path.Combine(directory, fileName);

        Directory.CreateDirectory(directory);
        using var stream = File.Create(fullPath);
        target.SaveAsPng(stream, target.Width, target.Height);
        return fullPath;
    }

    private static string SanitizeFileName(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        var result = name.ToCharArray();
        for (int i = 0; i < result.Length; i++)
        {
            if (Array.IndexOf(invalid, result[i]) >= 0)
                result[i] = '_';
        }
        return new string(result);
    }
}
