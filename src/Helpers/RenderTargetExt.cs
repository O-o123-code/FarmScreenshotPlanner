using Microsoft.Xna.Framework.Graphics;

namespace FarmScreenshotPlanner.Helpers;

public static class RenderTargetExt
{
    public static RenderTarget2D CreateRT(this GraphicsDevice gd, int width, int height)
    {
        return new RenderTarget2D(gd, width, height, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
    }

    public static byte[] ToPngBytes(this RenderTarget2D rt)
    {
        using var stream = new MemoryStream();
        rt.SaveAsPng(stream, rt.Width, rt.Height);
        return stream.ToArray();
    }
}
