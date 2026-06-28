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

            // === Step 1-2: Back + Buildings 地图图层 ===
            DrawMapLayer(displayDevice, mapSB, map, "Back");
            DrawMapLayer(displayDevice, mapSB, map, "Buildings");
            Logger?.Debug("Map layers (Back, Buildings) drawn.");

            // === Step 3: Front 地图图层（在实体之前绘制） ===
            // Front 层包含树冠、屋檐等，需要在实体下方作为背景
            DrawMapLayer(displayDevice, mapSB, map, "Front");
            Logger?.Debug("Map layer (Front) drawn before entities.");

            // === Step 4: 手动覆盖物 — 统一 Y 排序 ===
            // 所有实体收集到一个列表，按 Y 坐标排序后统一绘制
            // 这样 truffle(Y=50) 在果树(Y=37) 后面时，truffle 先绘制，果树后绘制覆盖
            var entities = new List<(int SortY, Action<SpriteBatch> Draw, string Kind)>();

            int tfCount = 0, objCount = 0, furCount = 0, buildCount = 0;

            // 4a: Terrain Features (耕地, 树木, 作物)
            foreach (var (tile, feature) in location.terrainFeatures.Pairs)
            {
                int y = (int)tile.Y;
                entities.Add((y, sb => feature.draw(Game1.spriteBatch), "TF"));
                tfCount++;
            }

            // 4b: Large Terrain Features
            foreach (var feature in location.largeTerrainFeatures)
            {
                int y = (int)feature.Tile.Y;
                entities.Add((y, sb => feature.draw(Game1.spriteBatch), "LTF"));
                tfCount++;
            }

            // 4c: Resource Clumps (仅农场)
            if (location is Farm farm)
            {
                foreach (var clump in farm.resourceClumps)
                {
                    int y = (int)clump.Tile.Y;
                    entities.Add((y, sb => clump.draw(Game1.spriteBatch), "RC"));
                    tfCount++;
                }
            }

            // 4d: Objects (松露, 洒水器, 熔炉等)
            foreach (var (tile, obj) in location.Objects.Pairs)
            {
                if (obj is null) continue;
                int tileX = (int)tile.X;
                int tileY = (int)tile.Y;

                var itemData = ItemRegistry.GetDataOrErrorItem(obj.QualifiedItemId);
                var tex = itemData.GetTexture();
                if (tex is null || tex.IsDisposed)
                {
                    objCount++;
                    continue;
                }
                var srcRect = itemData.GetSourceRect();
                var drawY = tileY * 64 - Math.Max(0, srcRect.Height * 4 - 64);
                var pos = new Vector2(tileX * 64, drawY);

                entities.Add((tileY, sb => Game1.spriteBatch.Draw(tex, pos, srcRect, Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0f), "Obj"));
                objCount++;
            }

            // 4e: Furniture (狗屋, 家具等)
            foreach (var furniture in location.furniture)
            {
                if (furniture is null) continue;
                int tileX = (int)furniture.TileLocation.X;
                int tileY = (int)furniture.TileLocation.Y;

                if (furniture is FishTankFurniture)
                {
                    entities.Add((tileY, sb => furniture.draw(Game1.spriteBatch, tileX, tileY, 1f), "Fur"));
                    furCount++;
                    continue;
                }

                var itemData = ItemRegistry.GetDataOrErrorItem(furniture.QualifiedItemId);
                var tex = itemData.GetTexture();
                if (tex is null || tex.IsDisposed)
                {
                    furCount++;
                    continue;
                }
                var srcRectVal = furniture.sourceRect.Value;
                var bbVal = furniture.boundingBox.Value;
                var effects = furniture.Flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                var fpos = new Vector2(bbVal.X, bbVal.Y - (srcRectVal.Height * 4 - bbVal.Height));

                entities.Add((tileY, sb => Game1.spriteBatch.Draw(tex, fpos, srcRectVal, Color.White, 0f, Vector2.Zero, 4f, effects, 0f), "Fur"));
                furCount++;
            }

            // 4f: Buildings (农舍, 谷仓等游戏建筑)
            foreach (var building in location.buildings)
            {
                // 使用建筑底部（tileY + tilesHigh）作为排序基准
                int sortY = building.tileY.Value + building.tilesHigh.Value;
                entities.Add((sortY, sb => building.draw(Game1.spriteBatch), "Bld"));
                buildCount++;
            }

            // 统一 Y 排序并绘制
            entities.Sort((a, b) => a.SortY.CompareTo(b.SortY));

            Game1.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            foreach (var (_, draw, _) in entities)
            {
                draw(Game1.spriteBatch);
            }
            Game1.spriteBatch.End();

            Logger?.Debug($"Entities drawn (Y-sorted). TF:{tfCount} Obj:{objCount} Fur:{furCount} Bld:{buildCount} Total:{entities.Count}");

            // === Step 5: AlwaysFront 地图图层（始终在最上层） ===
            DrawMapLayer(displayDevice, mapSB, map, "AlwaysFront");
            Logger?.Debug("AlwaysFront layer drawn.");
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
