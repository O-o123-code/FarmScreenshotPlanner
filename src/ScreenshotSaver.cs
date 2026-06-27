using Microsoft.Xna.Framework.Graphics;

namespace FarmScreenshotPlanner;

public class ScreenshotSaver
{
    public string Save(RenderTarget2D target, string directory, string prefix)
    {
        string fileName = $"{SanitizeFileName(prefix)}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        string fullPath = Path.Combine(directory, fileName);

        byte[] pngBytes;
        using (var stream = new MemoryStream())
        {
            target.SaveAsPng(stream, target.Width, target.Height);
            pngBytes = stream.ToArray();
        }

        Directory.CreateDirectory(directory);
        File.WriteAllBytes(fullPath, pngBytes);
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
