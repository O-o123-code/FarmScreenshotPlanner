using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace FarmBlueprint;

/// <summary>
/// 核心截图服务：分块渲染完整地图 + 网格叠加 + 保存。
/// </summary>
internal class BlueprintScreenshot
{
    private readonly IMonitor _monitor;
    private readonly IModHelper _helper;
    private readonly ModConfig _config;

    /// <summary>单块渲染尺寸（像素），不超过 GPU 纹理上限</summary>
    private const int ChunkSize = 2048;

    public BlueprintScreenshot(IMonitor monitor, IModHelper helper, ModConfig config)
    {
        _monitor = monitor;
        _helper = helper;
        _config = config;
    }

    /// <summary>
    /// 执行截图并异步保存。
    /// GPU 渲染必须在主线程，文件保存放在后台。
    /// </summary>
    public void CaptureAsync(CaptureArea area, Action<string, string> onComplete)
    {
        try
        {
            // 渲染部分必须在主线程（GPU 操作）
            var (texture, width, height, filePath) = CaptureOnMainThread(area);

            // 文件保存放在后台线程（CPU 操作，可能耗时）
            Task.Run(() =>
            {
                try
                {
                    using (var stream = File.OpenWrite(filePath))
                    {
                        texture.SaveAsPng(stream, width, height);
                    }
                    texture.Dispose();
                    onComplete(filePath, null);
                }
                catch (Exception ex)
                {
                    texture.Dispose();
                    _monitor.Log($"Save failed: {ex.Message}", LogLevel.Error);
                    onComplete(null, $"[Farm Blueprint] {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            _monitor.Log($"Screenshot failed: {ex.Message}", LogLevel.Error);
            onComplete(null, $"[Farm Blueprint] {ex.Message}");
        }
    }

    /// <summary>
    /// 在主线程执行渲染（GPU 操作）
    /// </summary>
    private (Texture2D texture, int width, int height, string filePath) CaptureOnMainThread(CaptureArea area)
    {
        var location = GetTargetLocation(area);
        if (location == null || location.Map == null)
            throw new InvalidOperationException("Target location not loaded.");

        // 计算地图尺寸
        int tilesWide = location.Map.Layers[0].LayerWidth;
        int tilesHigh = location.Map.Layers[0].LayerHeight;
        int pixelWidth = tilesWide * Game1.tileSize;
        int pixelHeight = tilesHigh * Game1.tileSize;

        _monitor.Log($"Capturing {location.Name}: {pixelWidth}x{pixelHeight}px ({tilesWide}x{tilesHigh} tiles)", LogLevel.Info);

        // 解析网格颜色
        var gridColor = ParseGridColor();

        // 分块渲染
        var fullImage = RenderTiled(location, pixelWidth, pixelHeight, gridColor);

        // 准备保存路径
        var saveDir = Path.Combine(_helper.DirectoryPath, "screenshots");
        Directory.CreateDirectory(saveDir);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var fileName = $"{_config.FileNamePrefix}_{timestamp}.png";
        var filePath = Path.Combine(saveDir, fileName);

        return (fullImage, pixelWidth, pixelHeight, filePath);
    }

    /// <summary>
    /// 分块渲染完整地图
    /// </summary>
    private Texture2D RenderTiled(GameLocation location, int totalWidth, int totalHeight, Color gridColor)
    {
        var device = Game1.graphics.GraphicsDevice;

        // 计算分块数
        int chunksX = (int)Math.Ceiling((double)totalWidth / ChunkSize);
        int chunksY = (int)Math.Ceiling((double)totalHeight / ChunkSize);

        _monitor.Log($"Rendering in {chunksX}x{chunksY} chunks", LogLevel.Info);

        // 创建最终合成纹理
        var fullTexture = new Texture2D(device, totalWidth, totalHeight);
        var allPixels = new Color[totalWidth * totalHeight];

        for (int cy = 0; cy < chunksY; cy++)
        {
            for (int cx = 0; cx < chunksX; cx++)
            {
                int chunkX = cx * ChunkSize;
                int chunkY = cy * ChunkSize;
                int chunkW = Math.Min(ChunkSize, totalWidth - chunkX);
                int chunkH = Math.Min(ChunkSize, totalHeight - chunkY);

                // 渲染当前块
                var chunkTexture = RenderChunk(location, chunkX, chunkY, chunkW, chunkH, gridColor);

                // 读取像素数据到最终数组
                var chunkPixels = new Color[chunkW * chunkH];
                chunkTexture.GetData(chunkPixels);

                for (int y = 0; y < chunkH; y++)
                {
                    int srcRow = y * chunkW;
                    int dstRow = (chunkY + y) * totalWidth + chunkX;
                    Array.Copy(chunkPixels, srcRow, allPixels, dstRow, chunkW);
                }

                chunkTexture.Dispose();
            }
        }

        // 写入完整图像
        fullTexture.SetData(allPixels);

        return fullTexture;
    }

    /// <summary>
    /// 渲染单个块
    /// </summary>
    private Texture2D RenderChunk(GameLocation location, int offsetX, int offsetY, int width, int height, Color gridColor)
    {
        var device = Game1.graphics.GraphicsDevice;
        var spriteBatch = Game1.spriteBatch;

        // 创建离屏渲染目标
        var target = new RenderTarget2D(device, width, height, false, SurfaceFormat.Color, DepthFormat.None);
        device.SetRenderTarget(target);
        device.Clear(Color.Transparent);

        // 保存原始视口
        var originalViewport = device.Viewport;

        // 设置相机偏移（通过调整视口实现）
        device.Viewport = new Viewport(-offsetX, -offsetY, width, height);

        // 开始绘制
        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            null,
            null);

        // 使用 xTile 的 Display 绘制地图（Game1.mapDisplayDevice 是 SpriteBatch 包装的 IDisplayDevice）
        var displayDevice = Game1.mapDisplayDevice;
        var mapViewport = new xTile.Dimensions.Rectangle(0, 0, device.PresentationParameters.BackBufferWidth, device.PresentationParameters.BackBufferHeight);
        location.Map.Draw(displayDevice, mapViewport);

        // 绘制地点特有元素（如 NPC、物体等）
        location.draw(spriteBatch);

        // 绘制网格
        if (_config.EnableGrid)
        {
            GridRenderer.Draw(
                spriteBatch,
                width,
                height,
                Game1.tileSize,
                gridColor,
                _config.GridThickness);
        }

        spriteBatch.End();

        // 恢复视口
        device.Viewport = originalViewport;
        device.SetRenderTarget(null);

        return target;
    }

    /// <summary>
    /// 根据配置获取目标区域
    /// </summary>
    private GameLocation GetTargetLocation(CaptureArea area)
    {
        switch (area)
        {
            case CaptureArea.Farm:
                return Game1.getFarm();
            case CaptureArea.Greenhouse:
                return Game1.getLocationFromName("Greenhouse");
            case CaptureArea.Island:
                return Game1.getLocationFromName("IslandWest");
            case CaptureArea.CurrentLocation:
            default:
                return Game1.currentLocation;
        }
    }

    /// <summary>
    /// 解析配置的网格颜色
    /// </summary>
    private Color ParseGridColor()
    {
        var hex = _config.GridColor.TrimStart('#');

        if (hex.Length == 8 &&
            int.TryParse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out var a) &&
            int.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var r) &&
            int.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var g) &&
            int.TryParse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
        {
            // 应用配置的透明度
            int opacity = MathHelper.Clamp(_config.GridOpacity, 0, 255);
            return new Color(r, g, b, opacity);
        }

        // 默认黑色半透明
        int defaultOpacity = MathHelper.Clamp(_config.GridOpacity, 0, 255);
        return new Color(0, 0, 0, defaultOpacity);
    }
}
