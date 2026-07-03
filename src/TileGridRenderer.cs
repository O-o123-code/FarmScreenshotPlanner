using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace FarmScreenshotPlanner;

public class TileGridRenderer
{
    private const int TileSize = 64;

    private static Texture2D? _pixel;

    private sealed record GridCacheKey(
        int Width, int Height, int SourceWidth,
        int Thickness, float Opacity, string Color);

    private GridCacheKey? _lastKey;
    private Texture2D? _cachedGridTexture;

    private static Texture2D GetOrCreatePixel(GraphicsDevice gd)
    {
        if (_pixel is null || _pixel.IsDisposed)
        {
            _pixel = new Texture2D(gd, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }
        return _pixel;
    }

    public RenderTarget2D Apply(Texture2D source, ModConfig config, int finalW, int finalH)
    {
        if (finalW < 1) finalW = 1;
        if (finalH < 1) finalH = 1;

        var gd = Game1.graphics.GraphicsDevice;
        var originalTargets = gd.GetRenderTargets();

        var finalRT = new RenderTarget2D(gd, finalW, finalH, false,
            SurfaceFormat.Color, DepthFormat.None, 0,
            RenderTargetUsage.PreserveContents);
        gd.SetRenderTarget(finalRT);
        gd.Clear(Color.Transparent);

        try
        {
            using var sb = new SpriteBatch(gd);
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None,
                RasterizerState.CullNone);
            sb.Draw(source, new Rectangle(0, 0, finalW, finalH), Color.White);
            sb.End();

            if (config.Grid.Enabled)
            {
                var gridTexture = GetOrCreateGridTexture(gd, finalW, finalH, source.Width, config);
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    SamplerState.LinearClamp, DepthStencilState.None,
                    RasterizerState.CullNone);
                sb.Draw(gridTexture, new Rectangle(0, 0, finalW, finalH), Color.White);
                sb.End();
            }
        }
        finally
        {
            gd.SetRenderTargets(originalTargets);
            source.Dispose();
        }

        return finalRT;
    }

    private Texture2D GetOrCreateGridTexture(GraphicsDevice gd, int width, int height, int sourceWidth, ModConfig config)
    {
        var key = new GridCacheKey(width, height, sourceWidth,
            config.Grid.Thickness, config.Grid.Opacity, config.Grid.Color);

        if (_cachedGridTexture is not null && _lastKey == key && !_cachedGridTexture.IsDisposed
            && _cachedGridTexture is not RenderTarget2D { IsContentLost: true })
        {
            return _cachedGridTexture;
        }

        _cachedGridTexture?.Dispose();
        _lastKey = key;
        _cachedGridTexture = CreateTiledGridTexture(gd, width, height, sourceWidth, config);
        return _cachedGridTexture;
    }

    /// <summary>
    /// Creates a tiled grid texture using a single-cell pattern with wrap addressing.
    /// This reduces hundreds of Draw Calls to a single tiled Draw.
    /// </summary>
    private static Texture2D CreateTiledGridTexture(GraphicsDevice gd, int width, int height, int sourceWidth, ModConfig config)
    {
        float actualScale = (float)width / sourceWidth;
        int tilePx = Math.Max(1, (int)(TileSize * actualScale));
        int thickness = config.Grid.Thickness;
        Color gridColor = ParseHexColor(config.Grid.Color, config.Grid.Opacity);

        // Create a single tile-cell pattern: transparent interior + grid lines on top/left edges
        var cellData = new Color[tilePx * tilePx];
        for (int y = 0; y < tilePx; y++)
        {
            for (int x = 0; x < tilePx; x++)
            {
                bool isGridLine = x < thickness || y < thickness;
                cellData[y * tilePx + x] = isGridLine ? gridColor : Color.Transparent;
            }
        }

        var cellTexture = new Texture2D(gd, tilePx, tilePx);
        cellTexture.SetData(cellData);

        // Render the tiled grid into a full-size texture using wrap mode
        var gridRT = new RenderTarget2D(gd, width, height, false,
            SurfaceFormat.Color, DepthFormat.None, 0,
            RenderTargetUsage.PreserveContents);
        var originalTargets = gd.GetRenderTargets();
        gd.SetRenderTarget(gridRT);
        gd.Clear(Color.Transparent);

        using var sb = new SpriteBatch(gd);
        var wrapSampler = new SamplerState
        {
            Filter = TextureFilter.Point,
            AddressU = TextureAddressMode.Wrap,
            AddressV = TextureAddressMode.Wrap
        };
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
            wrapSampler, DepthStencilState.None,
            RasterizerState.CullNone);

        // Tile the cell pattern across the full area — single Draw call with wrap mode
        sb.Draw(cellTexture, new Rectangle(0, 0, width, height),
            new Rectangle(0, 0, width, height), Color.White);

        sb.End();
        gd.SetRenderTargets(originalTargets);

        cellTexture.Dispose();
        return gridRT;
    }

    private static Color ParseHexColor(string hex, float opacity)
    {
        if (hex.Length != 8) return Color.Black * opacity;
        byte r = Convert.ToByte(hex[..2], 16);
        byte g = Convert.ToByte(hex[2..4], 16);
        byte b = Convert.ToByte(hex[4..6], 16);
        byte a = Convert.ToByte(hex[6..8], 16);
        return new Color(r, g, b) * opacity;
    }
}
