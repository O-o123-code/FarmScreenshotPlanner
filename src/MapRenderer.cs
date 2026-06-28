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

            Logger?.Debug("Map layers (Back, Buildings) drawn.");

            Logger?.Debug($"Viewport before entity drawing: ({Game1.viewport.X},{Game1.viewport.Y},{Game1.viewport.Width},{Game1.viewport.Height})");
            Logger?.Debug($"RT check before entity drawing: Set={gd.GetRenderTargets().Length > 0}");

            StardewValley.Object? diagObj = null;
            int diagObjX = 0, diagObjY = 0;
            Furniture? diagFur = null;
            int diagFurX = 0, diagFurY = 0;

            // ====== 阶段 A: Buildings + Terrain Features ======
            var tfList = new List<(int Y, Action Draw)>();
            int tfCount = 0;

            foreach (var building in location.buildings)
            {
                int y = building.tileY.Value;
                tfList.Add((y, () => building.draw(Game1.spriteBatch)));
            }

            foreach (var (tile, feature) in location.terrainFeatures.Pairs)
            {
                int y = (int)tile.Y;
                tfList.Add((y, () => feature.draw(Game1.spriteBatch)));

                if (feature is Tree tree)
                {
                    Logger?.Debug($"Tree: tile({tile.X},{tile.Y}) growthStage={tree.growthStage.Value} stump={tree.stump.Value}");
                    if (tree.growthStage.Value >= 5 && !tree.stump.Value)
                        tfList.Add((y + 1, () => feature.draw(Game1.spriteBatch)));
                }

                if (tfCount++ < 3)
                    Logger?.Debug($"  TF[{tfCount}]: type={feature.GetType().Name} tile({tile.X},{tile.Y})");
            }

            foreach (var feature in location.largeTerrainFeatures)
            {
                int y = (int)feature.Tile.Y;
                tfList.Add((y, () => feature.draw(Game1.spriteBatch)));
            }

            if (location is Farm farm)
            {
                foreach (var clump in farm.resourceClumps)
                {
                    int y = (int)clump.Tile.Y;
                    tfList.Add((y, () => clump.draw(Game1.spriteBatch)));
                }
            }

            tfList.Sort((a, b) => a.Y.CompareTo(b.Y));
            foreach (var (_, draw) in tfList)
            {
                Game1.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                draw();
                Game1.spriteBatch.End();
            }

            Logger?.Debug($"Terrain features drawn. Count: {tfList.Count}");

            // ====== 阶段 B: Objects ======
            int objCount = 0;
            var objList = new List<(int Y, Action Draw)>();

            foreach (var (tile, obj) in location.Objects.Pairs)
            {
                if (obj is null) continue;
                int y = (int)tile.Y;
                int tileX = (int)tile.X;
                int tileY = (int)tile.Y;
                var itemData = ItemRegistry.GetDataOrErrorItem(obj.QualifiedItemId);
                var tex = itemData.GetTexture();
                if (tex is null || tex.IsDisposed)
                {
                    Logger?.Debug($"  Object[{objCount + 1}]: tile({tileX},{tileY}) name={obj.Name} SKIP - texture null/disposed");
                    objCount++;
                    continue;
                }
                var srcRect = itemData.GetSourceRect();
                var drawY = tileY * 64 - Math.Max(0, srcRect.Height * 4 - 64);
                var pos = new Vector2(tileX * 64, drawY);
                objList.Add((y, () => Game1.spriteBatch.Draw(tex, pos, srcRect, Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0f)));

                if (objCount < 5)
                    Logger?.Debug($"  Object[{objCount + 1}]: tile({tileX},{tileY}) type={obj.GetType().Name} name={obj.Name} qid={obj.QualifiedItemId} srcRect=({srcRect.X},{srcRect.Y},{srcRect.Width},{srcRect.Height}) directDraw");

                if (objCount == 0)
                {
                    diagObj = obj;
                    diagObjX = tileX * 64;
                    diagObjY = drawY;
                    Logger?.Debug($"  [DBG] First object saved for diagnostic: name={obj.Name} pos=({diagObjX},{diagObjY})");
                }
                objCount++;
            }

            objList.Sort((a, b) => a.Y.CompareTo(b.Y));
            foreach (var (_, draw) in objList)
            {
                Game1.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                draw();
                Game1.spriteBatch.End();
            }

            Logger?.Debug($"Objects drawn. Count: {objCount}");

            // ====== 阶段 C: Furniture ======
            int furCount = 0;
            var furList = new List<(int Y, Action Draw)>();

            foreach (var furniture in location.furniture)
            {
                if (furniture is null) continue;
                int y = (int)furniture.TileLocation.Y;
                int tileX = (int)furniture.TileLocation.X;
                int tileY = (int)furniture.TileLocation.Y;

                // FishTankFurniture: 回退到原 draw()，让其内部绘制鱼
                if (furniture is FishTankFurniture)
                {
                    furList.Add((y, () => furniture.draw(Game1.spriteBatch, tileX, tileY, 1f)));
                    furCount++;
                    continue;
                }

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
                var effects = furniture.Flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                var pos = new Vector2(bbVal.X, bbVal.Y - (srcRectVal.Height * 4 - bbVal.Height));
                furList.Add((y, () => Game1.spriteBatch.Draw(tex, pos, srcRectVal, Color.White, 0f, Vector2.Zero, 4f, effects, 0f)));

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

            furList.Sort((a, b) => a.Y.CompareTo(b.Y));
            foreach (var (_, draw) in furList)
            {
                Game1.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                draw();
                Game1.spriteBatch.End();
            }

            Logger?.Debug($"Furniture drawn. Count: {furCount}");

            // ====== 阶段 D: Front + AlwaysFront 图层（在实体之上） ======
            DrawMapLayer(displayDevice, mapSB, map, "Front");
            DrawMapLayer(displayDevice, mapSB, map, "AlwaysFront");
            Logger?.Debug("Front + AlwaysFront layers drawn after entities.");

            // ====== 诊断标记（使用 manualSB，不由 Game1.spriteBatch 管理） ======
            using var manualSB = new SpriteBatch(gd);
            manualSB.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);

            manualSB.Draw(Game1.staminaRect, new Microsoft.Xna.Framework.Rectangle(0, 0, 128, 128), Color.Magenta);
            manualSB.Draw(Game1.staminaRect, new Microsoft.Xna.Framework.Rectangle(64, 64, 64, 64), Color.Cyan);

            if (diagObj is not null)
            {
                manualSB.Draw(Game1.staminaRect, new Microsoft.Xna.Framework.Rectangle(diagObjX - 128, diagObjY, 64, 64), Color.Yellow * 0.5f);
                Logger?.Debug($"  [DIAG] Yellow marker at Object({diagObjX - 128},{diagObjY}) name={diagObj.Name}");

                var diagItemData = ItemRegistry.GetDataOrErrorItem(diagObj.QualifiedItemId);
                var diagTex = diagItemData.GetTexture();
                if (diagTex is not null && !diagTex.IsDisposed)
                {
                    var diagSrcRect = diagItemData.GetSourceRect();
                    var diagDrawY = diagObjY - Math.Max(0, diagSrcRect.Height * 4 - 64);
                    manualSB.Draw(diagTex, new Vector2(diagObjX, diagDrawY + 96), diagSrcRect, Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0f);
                    Logger?.Debug($"  [DIAG] Direct texture draw for obj qid={diagObj.QualifiedItemId}");
                }
            }

            if (diagFur is not null)
            {
                manualSB.Draw(Game1.staminaRect, new Microsoft.Xna.Framework.Rectangle(diagFurX - 128, diagFurY, 64, 64), Color.Red * 0.5f);
                Logger?.Debug($"  [DIAG] Red marker at Furniture({diagFurX - 128},{diagFurY}) name={diagFur.Name}");

                var diagFurData = ItemRegistry.GetDataOrErrorItem(diagFur.QualifiedItemId);
                var diagFurTex = diagFurData.GetTexture();
                if (diagFurTex is not null && !diagFurTex.IsDisposed)
                {
                    var diagFurSrc = diagFur.sourceRect.Value;
                    var diagFurBB = diagFur.boundingBox.Value;
                    var diagFurPos = new Vector2(diagFurBB.X, diagFurBB.Y - (diagFurSrc.Height * 4 - diagFurBB.Height));
                    manualSB.Draw(diagFurTex, new Vector2(diagFurPos.X, diagFurPos.Y + 128), diagFurSrc, Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0f);
                    Logger?.Debug($"  [DIAG] Direct texture draw for furniture qid={diagFur.QualifiedItemId}");
                }
            }

            manualSB.End();

            Logger?.Debug($"Entity drawing completed. TF:{tfList.Count} O:{objCount} F:{furCount}");
            Logger?.Debug($"RT check after entity drawing: Set={gd.GetRenderTargets().Length > 0}");

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
