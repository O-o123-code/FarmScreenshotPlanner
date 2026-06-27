using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using xTile;
using xTile.Dimensions;
using xTile.Display;
using xTile.Layers;

namespace FarmScreenshotPlanner;

public record MapRenderResult(RenderTarget2D FullRT, int MapPixelW, int MapPixelH);

public class MapRenderer
{
    public MapRenderResult Render(GameLocation location)
    {
        var map = location.Map;
        int mapPixelW = map.Layers[0].LayerWidth * 64;
        int mapPixelH = map.Layers[0].LayerHeight * 64;

        var gd = Game1.graphics.GraphicsDevice;
        var originalTargets = gd.GetRenderTargets();

        var fullRT = new RenderTarget2D(gd, mapPixelW, mapPixelH, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        gd.SetRenderTarget(fullRT);
        gd.Clear(Color.White);

        var prevViewport = Game1.viewport;
        Game1.viewport = new xTile.Dimensions.Rectangle(0, 0, mapPixelW, mapPixelH);

        try
        {
            var displayDevice = Game1.mapDisplayDevice;
            using var mapSB = new SpriteBatch(gd);

            DrawMapLayer(displayDevice, mapSB, map, "Back");
            DrawMapLayer(displayDevice, mapSB, map, "Buildings");
            DrawMapLayer(displayDevice, mapSB, map, "Front");

            var drawItems = new List<(int Y, Action Draw)>();
            var manualSB = new SpriteBatch(gd);
            manualSB.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);

            foreach (var building in location.buildings)
            {
                int y = building.tileY.Value;
                drawItems.Add((y, () => building.draw(manualSB)));
            }

            foreach (var (tile, feature) in location.terrainFeatures.Pairs)
            {
                int y = (int)tile.Y;
                drawItems.Add((y, () => feature.draw(manualSB)));
            }

            foreach (var feature in location.largeTerrainFeatures)
            {
                int y = (int)feature.Tile.Y;
                drawItems.Add((y, () => feature.draw(manualSB)));
            }

            if (location is Farm farm)
            {
                foreach (var clump in farm.resourceClumps)
                {
                    int y = (int)clump.Tile.Y;
                    drawItems.Add((y, () => clump.draw(manualSB)));
                }
            }

            foreach (var (tile, obj) in location.Objects.Pairs)
            {
                int y = (int)tile.Y;
                int x = (int)tile.X * 64;
                drawItems.Add((y, () => obj.draw(manualSB, x, y * 64)));
            }

            foreach (var furniture in location.furniture)
            {
                int y = (int)furniture.TileLocation.Y;
                int x = (int)furniture.TileLocation.X * 64;
                drawItems.Add((y, () => furniture.draw(manualSB, x, y * 64)));
            }

            drawItems.Sort((a, b) => a.Y.CompareTo(b.Y));
            foreach (var (_, draw) in drawItems)
            {
                draw();
            }

            manualSB.End();

            DrawMapLayer(displayDevice, mapSB, map, "AlwaysFront");
        }
        finally
        {
            Game1.viewport = prevViewport;
            gd.SetRenderTargets(originalTargets);
        }

        return new MapRenderResult(fullRT, mapPixelW, mapPixelH);
    }

    private static void DrawMapLayer(IDisplayDevice displayDevice, SpriteBatch spriteBatch, Map map, string layerId)
    {
        var layer = map.GetLayer(layerId);
        if (layer is null) return;

        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
        displayDevice.BeginScene(spriteBatch);
        for (int tx = 0; tx < layer.LayerWidth; tx++)
        {
            for (int ty = 0; ty < layer.LayerHeight; ty++)
            {
                var tile = layer.Tiles[tx, ty];
                if (tile is null) continue;
                displayDevice.DrawTile(tile, new Location(tx * 64, ty * 64), 0f);
            }
        }
        displayDevice.EndScene();
        spriteBatch.End();
    }
}
