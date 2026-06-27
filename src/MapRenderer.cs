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
            Logger?.Debug($"RT check before manual batch: Set={gd.GetRenderTargets().Length > 0}");

            // 使用本地 SpriteBatch（离屏渲染标准实践）
            using var manualSB = new SpriteBatch(gd);

            // ====== 收集阶段：收集所有需要绘制的实体，同时保存首个实体用于诊断 ======
            var drawItems = new List<(int Y, Action Draw)>();
            int tfCount = 0;

            StardewValley.Object? diagObj = null;
            int diagObjX = 0, diagObjY = 0;
            Furniture? diagFur = null;
            int diagFurX = 0, diagFurY = 0;

            foreach (var building in location.buildings)
            {
                int y = building.tileY.Value;
                drawItems.Add((y, () => building.draw(Game1.spriteBatch)));
            }

            foreach (var (tile, feature) in location.terrainFeatures.Pairs)
            {
                int y = (int)tile.Y;
                drawItems.Add((y, () => feature.draw(Game1.spriteBatch)));

                if (feature is Tree tree)
                {
                    Logger?.Debug($"Tree: tile({tile.X},{tile.Y}) growthStage={tree.growthStage.Value} stump={tree.stump.Value}");
                    if (tree.growthStage.Value >= 5 && !tree.stump.Value)
                        drawItems.Add((y + 1, () => feature.draw(Game1.spriteBatch)));
                }

                if (tfCount++ < 3)
                    Logger?.Debug($"  TF[{tfCount}]: type={feature.GetType().Name} tile({tile.X},{tile.Y})");
            }

            foreach (var feature in location.largeTerrainFeatures)
            {
                int y = (int)feature.Tile.Y;
                drawItems.Add((y, () => feature.draw(Game1.spriteBatch)));
            }

            if (location is Farm farm)
            {
                foreach (var clump in farm.resourceClumps)
                {
                    int y = (int)clump.Tile.Y;
                    drawItems.Add((y, () => clump.draw(Game1.spriteBatch)));
                }
            }

            int objCount = 0;
            foreach (var (tile, obj) in location.Objects.Pairs)
            {
                if (obj is null) continue;
                int y = (int)tile.Y;
                int tileX = (int)tile.X;
                int tileY = (int)tile.Y;
                var srcRect = Game1.getSourceRectForStandardTileSheet(Game1.objectSpriteSheet, obj.ParentSheetIndex, 16, 16);
                var pos = new Vector2(tileX * 64, tileY * 64);
                drawItems.Add((y, () => Game1.spriteBatch.Draw(Game1.objectSpriteSheet, pos, srcRect, Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0f)));

                if (objCount < 5)
                    Logger?.Debug($"  Object[{objCount + 1}]: tile({tileX},{tileY}) type={obj.GetType().Name} name={obj.Name} parentSheetIndex={obj.ParentSheetIndex} category={obj.Category} directDraw");

                if (objCount == 0)
                {
                    diagObj = obj;
                    diagObjX = tileX * 64;
                    diagObjY = tileY * 64;
                    Logger?.Debug($"  [DBG] First object saved for diagnostic: name={obj.Name} pos=({diagObjX},{diagObjY})");
                }
                objCount++;
            }

            int furCount = 0;
            foreach (var furniture in location.furniture)
            {
                if (furniture is null) continue;
                int y = (int)furniture.TileLocation.Y;
                int tileX = (int)furniture.TileLocation.X;
                int tileY = (int)furniture.TileLocation.Y;
                var itemData = ItemRegistry.GetDataOrErrorItem(furniture.QualifiedItemId);
                var tex = itemData.GetTexture();
                if (tex is null || tex.IsDisposed)
                {
                    Logger?.Debug($"  Furniture[{furCount + 1}]: tile({tileX},{tileY}) name={furniture.Name} SKIP - texture null/disposed");
                    furCount++;
                    continue;
                }
                var srcRectVal = furniture.sourceRect.Value;
                var bbVal = furniture.boundingBox.Value;
                var pos = new Vector2(bbVal.X, bbVal.Y - (srcRectVal.Height * 4 - bbVal.Height));
                drawItems.Add((y, () => Game1.spriteBatch.Draw(tex, pos, srcRectVal, Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0f)));

                if (furCount < 5)
                    Logger?.Debug($"  Furniture[{furCount + 1}]: tile({tileX},{tileY}) type={furniture.GetType().Name} name={furniture.Name} srcRect=({srcRectVal.X},{srcRectVal.Y},{srcRectVal.Width},{srcRectVal.Height}) bb=({bbVal.X},{bbVal.Y},{bbVal.Width},{bbVal.Height}) directDraw");

                if (furCount == 0)
                {
                    diagFur = furniture;
                    diagFurX = tileX * 64;
                    diagFurY = tileY * 64;
                    Logger?.Debug($"  [DBG] First furniture saved for diagnostic: name={furniture.Name} pos=({diagFurX},{diagFurY})");
                }
                furCount++;
            }

            Logger?.Debug($"Manual overrides collected: B:{location.buildings.Count} TF:{tfCount} LTF:{location.largeTerrainFeatures.Count} O:{objCount} F:{furCount}");

            // ====== 分阶段绘制 ======
            // 阶段 1: 每个实体独立 Begin/End（防止实体内部 End/Begin 干扰外部批次状态）
            drawItems.Sort((a, b) => a.Y.CompareTo(b.Y));
            int execIndex = 0;
            foreach (var (_, draw) in drawItems)
            {
                Game1.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                draw();
                Game1.spriteBatch.End();
                execIndex++;
            }

            // 阶段 2: 使用 manualSB 绘制诊断标记（始终可靠）
            manualSB.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);

            manualSB.Draw(Game1.staminaRect, new Microsoft.Xna.Framework.Rectangle(0, 0, 128, 128), Color.Magenta);
            manualSB.Draw(Game1.staminaRect, new Microsoft.Xna.Framework.Rectangle(64, 64, 64, 64), Color.Cyan);

            if (diagObj is not null)
            {
                manualSB.Draw(Game1.staminaRect, new Microsoft.Xna.Framework.Rectangle(diagObjX - 128, diagObjY, 64, 64), Color.Yellow * 0.5f);
                Logger?.Debug($"  [DIAG] Yellow marker at Object({diagObjX - 128},{diagObjY}) name={diagObj.Name}");

                if (Game1.objectSpriteSheet is not null && !Game1.objectSpriteSheet.IsDisposed)
                {
                    var srcRect = Game1.getSourceRectForStandardTileSheet(Game1.objectSpriteSheet, diagObj.ParentSheetIndex, 16, 16);
                    manualSB.Draw(Game1.objectSpriteSheet, new Vector2(diagObjX, diagObjY + 96), srcRect, Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0f);
                    Logger?.Debug($"  [DIAG] Direct texture draw for obj idx={diagObj.ParentSheetIndex}");
                }
            }

            if (diagFur is not null)
            {
                manualSB.Draw(Game1.staminaRect, new Microsoft.Xna.Framework.Rectangle(diagFurX - 128, diagFurY, 64, 64), Color.Red * 0.5f);
                Logger?.Debug($"  [DIAG] Red marker at Furniture({diagFurX - 128},{diagFurY}) name={diagFur.Name}");
            }

            manualSB.End();

            Logger?.Debug($"Manual overrides drawn. Total drawItems executed: {execIndex}");
            Logger?.Debug($"RT check after manual batch: Set={gd.GetRenderTargets().Length > 0}");

            // ====== 诊断 6: 像素质检测（采样图块中心，避免透明边角）=====
            gd.SetRenderTargets(originalTargets);
            try
            {
                if (diagObj is not null)
                {
                    Color[] pixel = new Color[1];
                    int cx = diagObjX + 32, cy = diagObjY + 32; // 图块中心

                    // 地板参考色（距实体 3 tiles 外，确认地板底色）
                    fullRT.GetData(0, new Microsoft.Xna.Framework.Rectangle(diagObjX + 192, diagObjY, 1, 1), pixel, 0, 1);
                    Logger?.Debug($"  [PIXEL] Floor ref ({diagObjX + 192},{diagObjY}): R={pixel[0].R} G={pixel[0].G} B={pixel[0].B} A={pixel[0].A}");

                    // Object 图块中心（实体绘制区域）
                    fullRT.GetData(0, new Microsoft.Xna.Framework.Rectangle(cx, cy, 1, 1), pixel, 0, 1);
                    Logger?.Debug($"  [PIXEL] Object CENTER ({cx},{cy}): R={pixel[0].R} G={pixel[0].G} B={pixel[0].B} A={pixel[0].A}");

                    // DirectDraw 纹理图块中心
                    fullRT.GetData(0, new Microsoft.Xna.Framework.Rectangle(cx, cy + 96, 1, 1), pixel, 0, 1);
                    Logger?.Debug($"  [PIXEL] DirectDraw CENTER ({cx},{cy + 96}): R={pixel[0].R} G={pixel[0].G} B={pixel[0].B} A={pixel[0].A}");
                }
            }
            catch (Exception ex)
            {
                Logger?.Debug($"  [PIXEL] Readback failed: {ex.Message}");
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
