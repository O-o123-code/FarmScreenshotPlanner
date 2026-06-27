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
    public RollingFileLogger? Logger { get; set; }

    public MapRenderResult Render(GameLocation location)
    {
        Logger?.Debug($"MapRenderer.Render started for location: {location.Name ?? "null"}");

        var map = location.Map;
        int mapPixelW = map.Layers[0].LayerWidth * 64;
        int mapPixelH = map.Layers[0].LayerHeight * 64;

        var gd = Game1.graphics.GraphicsDevice;
        var originalTargets = gd.GetRenderTargets();

        var fullRT = new RenderTarget2D(gd, mapPixelW, mapPixelH, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        gd.SetRenderTarget(fullRT);
        gd.Clear(Color.White);

        Logger?.Debug($"fullRT created: {mapPixelW}x{mapPixelH}");

        var prevViewport = Game1.viewport;
        Game1.viewport = new xTile.Dimensions.Rectangle(0, 0, mapPixelW, mapPixelH);

        try
        {
            var displayDevice = Game1.mapDisplayDevice;
            using var mapSB = new SpriteBatch(gd);

            DrawMapLayer(displayDevice, mapSB, map, "Back");
            DrawMapLayer(displayDevice, mapSB, map, "Buildings");
            DrawMapLayer(displayDevice, mapSB, map, "Front");
            DrawMapLayer(displayDevice, mapSB, map, "AlwaysFront");

            Logger?.Debug("Map layers (Back, Buildings, Front, AlwaysFront) drawn.");

            Logger?.Debug($"Viewport before manual batch: ({Game1.viewport.X},{Game1.viewport.Y},{Game1.viewport.Width},{Game1.viewport.Height})");
            Logger?.Debug($"RT check: Set={gd.GetRenderTargets().Length > 0}");

            var drawItems = new List<(int Y, Action Draw)>();
            int tfCount = 0;

            foreach (var building in location.buildings)
            {
                int y = building.tileY.Value;
                drawItems.Add((y, () => building.draw(mapSB)));
            }

            foreach (var (tile, feature) in location.terrainFeatures.Pairs)
            {
                int y = (int)tile.Y;
                drawItems.Add((y, () => feature.draw(mapSB)));

                if (feature is Tree tree)
                {
                    Logger?.Debug($"Tree: tile({tile.X},{tile.Y}) growthStage={tree.growthStage.Value} stump={tree.stump.Value}");
                    if (tree.growthStage.Value >= 5 && !tree.stump.Value)
                        drawItems.Add((y + 1, () => feature.draw(mapSB)));
                }

                if (tfCount++ < 3)
                    Logger?.Debug($"  TF[{tfCount}]: type={feature.GetType().Name} tile({tile.X},{tile.Y})");
            }

            foreach (var feature in location.largeTerrainFeatures)
            {
                int y = (int)feature.Tile.Y;
                drawItems.Add((y, () => feature.draw(mapSB)));
            }

            if (location is Farm farm)
            {
                foreach (var clump in farm.resourceClumps)
                {
                    int y = (int)clump.Tile.Y;
                    drawItems.Add((y, () => clump.draw(mapSB)));
                }
            }

            int objCount = 0;
            foreach (var (tile, obj) in location.Objects.Pairs)
            {
                if (obj is null) continue;
                int y = (int)tile.Y;
                int x = (int)tile.X * 64;
                int py = (int)tile.Y * 64;
                drawItems.Add((y, () => obj.draw(mapSB, x, py, 1f)));

                if (objCount++ < 5)
                    Logger?.Debug($"  Object[{objCount}]: tile({tile.X},{tile.Y}) type={obj.GetType().Name} name={obj.Name} parentSheetIndex={obj.ParentSheetIndex} category={obj.Category}");
            }

            int furCount = 0;
            foreach (var furniture in location.furniture)
            {
                if (furniture is null) continue;
                int y = (int)furniture.TileLocation.Y;
                int x = (int)furniture.TileLocation.X * 64;
                int py = (int)furniture.TileLocation.Y * 64;
                drawItems.Add((y, () => furniture.draw(mapSB, x, py, 1f)));

                if (furCount++ < 5)
                    Logger?.Debug($"  Furniture[{furCount}]: tile({furniture.TileLocation.X},{furniture.TileLocation.Y}) type={furniture.GetType().Name} name={furniture.Name}");
            }

            Logger?.Debug($"Manual overrides collected: B:{location.buildings.Count} TF:{tfCount} LTF:{location.largeTerrainFeatures.Count} O:{objCount} F:{furCount}");

            mapSB.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);

            mapSB.Draw(Game1.staminaRect, new Microsoft.Xna.Framework.Rectangle(0, 0, 128, 128), Color.Magenta);

            drawItems.Sort((a, b) => a.Y.CompareTo(b.Y));
            foreach (var (_, draw) in drawItems)
            {
                draw();
            }
            mapSB.End();

            Logger?.Debug("Manual overrides drawn.");
        }
        finally
        {
            Game1.viewport = prevViewport;
            gd.SetRenderTargets(originalTargets);
        }

        Logger?.Debug("MapRenderer.Render completed.");
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
