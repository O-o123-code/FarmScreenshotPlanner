using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace FarmScreenshotPlanner;

public record MapRenderResult(RenderTarget2D FullRT, int MapPixelW, int MapPixelH);

public class MapRenderer
{
    public RollingFileLogger? Logger { get; set; }

    /// <summary>
    /// 使用分块视口 + GameLocation.draw() 渲染完整地图。
    /// 利用游戏自身的渲染管线处理所有图层（Back/Buildings/Front/实体/AlwaysFront），
    /// 确保树木、建筑等复杂精灵的渲染与游戏画面完全一致，
    /// 彻底消除手动图层渲染中 display device 与实体精灵的坐标系偏移问题。
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
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            int screenW = Game1.graphics.PreferredBackBufferWidth;
            int screenH = Game1.graphics.PreferredBackBufferHeight;

            gd.SetRenderTarget(fullRT);
            gd.Clear(Color.White);

            // 暂时隐藏角色/NPC/动物，避免它们出现在截图中
            var charBackup = new List<NPC>(location.characters);
            location.characters.Clear();
            Logger?.Debug($"Hidden {charBackup.Count} characters for screenshot.");

            // 如果是农场，隐藏动物
            List<FarmAnimal>? animalBackup = null;
            if (location is Farm farm)
            {
                animalBackup = new List<FarmAnimal>(farm.animals.Values);
                farm.animals.Clear();
                Logger?.Debug($"Hidden {animalBackup.Count} farm animals.");
            }

            try
            {
                int chunksX = (mapPixelW + screenW - 1) / screenW;
                int chunksY = (mapPixelH + screenH - 1) / screenH;
                int totalChunks = chunksX * chunksY;
                Logger?.Debug($"Rendering in {chunksX}x{chunksY}={totalChunks} chunks, screen={screenW}x{screenH}");

                // 复用单个 chunkRT 和 SpriteBatch，避免每个区块重复分配 GPU 资源
                var chunkRT = new RenderTarget2D(gd, screenW, screenH, false,
                    SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.DiscardContents);
                using var copySB = new SpriteBatch(gd);

                try
                {
                    int chunkIndex = 0;
                    for (int cy = 0; cy < chunksY; cy++)
                    {
                        for (int cx = 0; cx < chunksX; cx++)
                        {
                            chunkIndex++;
                            int vpX = cx * screenW;
                            int vpY = cy * screenH;
                            int chunkW = Math.Min(screenW, mapPixelW - vpX);
                            int chunkH = Math.Min(screenH, mapPixelH - vpY);

                            Game1.viewport = new xTile.Dimensions.Rectangle(vpX, vpY, chunkW, chunkH);

                            gd.SetRenderTarget(chunkRT);
                            gd.Clear(Color.Transparent);

                            try
                            {
                                location.draw(Game1.spriteBatch);
                            }
                            catch (Exception ex)
                            {
                                Logger?.Error($"location.draw() failed at chunk ({cx},{cy}): {ex.Message}");
                            }

                            // 将区块复制到完整 RT 的正确位置
                            gd.SetRenderTarget(fullRT);
                            copySB.Begin(SpriteSortMode.Immediate, BlendState.Opaque);
                            copySB.Draw(chunkRT, new Vector2(vpX, vpY),
                                new Microsoft.Xna.Framework.Rectangle(0, 0, chunkW, chunkH), Color.White);
                            copySB.End();
                        }
                    }
                }
                finally
                {
                    chunkRT.Dispose();
                }

                Logger?.Debug($"All {totalChunks} chunks rendered in {sw.ElapsedMilliseconds}ms.");
            }
            finally
            {
                // 恢复角色
                foreach (var c in charBackup)
                    location.characters.Add(c);
                Logger?.Debug($"Restored {charBackup.Count} characters.");

                // 恢复动物
                if (animalBackup is not null && location is Farm farm2)
                {
                    foreach (var a in animalBackup)
                        farm2.animals[a.myID.Value] = a;
                    Logger?.Debug($"Restored {animalBackup.Count} farm animals.");
                }
            }
        }
        finally
        {
            sw.Stop();
            Game1.viewport = prevViewport;
            gd.SetRenderTargets(originalTargets);
        }

        Logger?.Debug($"MapRenderer.Render completed in {sw.ElapsedMilliseconds}ms total.");
        return new MapRenderResult(fullRT, mapPixelW, mapPixelH);
    }
}
