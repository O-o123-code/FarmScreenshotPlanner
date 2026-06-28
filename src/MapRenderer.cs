using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using xTile;
using xTile.Dimensions;
using xTile.Display;
using xTile.Layers;

namespace FarmScreenshotPlanner;

public record MapRenderResult(RenderTarget2D FullRT, int MapPixelW, int MapPixelH);

public class MapRenderer
{
    public RollingFileLogger? Logger { get; set; }

    /// <summary>
    /// 单次全视口渲染：
    /// - 地图瓦片层 (Back/Buildings/Front/AlwaysFront) 通过 display device 绘制
    /// - 实体层通过 GameLocation.draw() 由游戏自身管线绘制
    /// 两者共享同一全图视口，坐标系一致。
    /// </summary>
    public MapRenderResult Render(GameLocation location)
    {
        Logger?.Debug($"MapRenderer.Render started for location: {location.Name ?? "null"}");

        var map = location.Map;
        int mapPixelW = map.Layers[0].LayerWidth * 64;
        int mapPixelH = map.Layers[0].LayerHeight * 64;

        var gd = Game1.graphics.GraphicsDevice;
        var originalTargets = gd.GetRenderTargets();

        var fullRT = new RenderTarget2D(gd, mapPixelW, mapPixelH, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);

        Logger?.Debug($"fullRT created: {mapPixelW}x{mapPixelH}");

        var prevViewport = Game1.viewport;
        Game1.viewport = new xTile.Dimensions.Rectangle(0, 0, mapPixelW, mapPixelH);

        try
        {
            var displayDevice = Game1.mapDisplayDevice;

            gd.SetRenderTarget(fullRT);
            gd.Clear(Color.White);

            // 暂时隐藏角色/NPC/动物
            var charBackup = new List<NPC>(location.characters);
            location.characters.Clear();

            List<FarmAnimal>? animalBackup = null;
            if (location is Farm farm)
            {
                animalBackup = new List<FarmAnimal>(farm.animals.Values);
                farm.animals.Clear();
            }
            Logger?.Debug($"Hidden {charBackup.Count} chars, {animalBackup?.Count ?? 0} animals.");

            try
            {
                // 1) Back + Buildings 瓦片层
                DrawMapLayer(displayDevice, Game1.spriteBatch, map, "Back");
                DrawMapLayer(displayDevice, Game1.spriteBatch, map, "Buildings");
                Logger?.Debug("Back + Buildings drawn.");

                // 2) 实体层 (TerrainFeature / Object / Character / Building)
                try
                {
                    Game1.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                        SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                    location.draw(Game1.spriteBatch);
                    Game1.spriteBatch.End();
                    Logger?.Debug("Entities drawn via location.draw().");
                }
                catch (Exception ex)
                {
                    try { Game1.spriteBatch.End(); } catch { }
                    Logger?.Error($"location.draw() failed: {ex.Message}");
                }

                // 3) Front + AlwaysFront 瓦片层
                DrawMapLayer(displayDevice, Game1.spriteBatch, map, "Front");
                DrawMapLayer(displayDevice, Game1.spriteBatch, map, "AlwaysFront");
                Logger?.Debug("Front + AlwaysFront drawn.");
            }
            finally
            {
                foreach (var c in charBackup)
                    location.characters.Add(c);
                if (animalBackup is not null && location is Farm farm2)
                    foreach (var a in animalBackup)
                        farm2.animals[a.myID.Value] = a;
            }
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

        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
            SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
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
