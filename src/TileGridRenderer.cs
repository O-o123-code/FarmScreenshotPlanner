using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace FarmScreenshotPlanner;

public class TileGridRenderer
{
    private static Texture2D? _pixel;

    private static Texture2D GetOrCreatePixel(GraphicsDevice gd)
    {
        if (_pixel is null || _pixel.IsDisposed)
        {
            _pixel = new Texture2D(gd, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }
        return _pixel;
    }

    public RenderTarget2D Apply(RenderTarget2D fullRT, ModConfig config, int finalW, int finalH, int mapPixelW, int mapPixelH)
    {
        if (finalW < 1) finalW = 1;
        if (finalH < 1) finalH = 1;

        var gd = Game1.graphics.GraphicsDevice;
        var originalTargets = gd.GetRenderTargets();

        var finalRT = new RenderTarget2D(gd, finalW, finalH, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        gd.SetRenderTarget(finalRT);
        gd.Clear(Color.Transparent);

        try
        {
            using var sb = new SpriteBatch(gd);
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            sb.Draw(fullRT, new Rectangle(0, 0, finalW, finalH), Color.White);
            sb.End();

            if (config.Grid.Enabled)
            {
                float actualScale = (float)finalW / mapPixelW;
                int tilePx = (int)(64 * actualScale);
                if (tilePx < 1) tilePx = 1;

                var pixel = GetOrCreatePixel(gd);
                Color gridColor = ParseHexColor(config.Grid.Color, config.Grid.Opacity);

                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

                for (int x = 0; x <= finalW; x += tilePx)
                {
                    sb.Draw(pixel, new Rectangle(x, 0, config.Grid.Thickness, finalH), gridColor);
                }

                for (int y = 0; y <= finalH; y += tilePx)
                {
                    sb.Draw(pixel, new Rectangle(0, y, finalW, config.Grid.Thickness), gridColor);
                }

                sb.End();
            }
        }
        finally
        {
            gd.SetRenderTargets(originalTargets);
            fullRT.Dispose();
        }

        return finalRT;
    }

    private static Color ParseHexColor(string hex, float opacity)
    {
        if (hex.Length != 8) return Color.Black * opacity;
        byte r = Convert.ToByte(hex[..2], 16);
        byte g = Convert.ToByte(hex[2..4], 16);
        byte b = Convert.ToByte(hex[4..6], 16);
        byte a = Convert.ToByte(hex[6..8], 16);
        return new Color(r, g, b) * (a / 255f * opacity);
    }
}
