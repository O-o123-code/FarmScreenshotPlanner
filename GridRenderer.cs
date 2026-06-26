using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace FarmBlueprint;

/// <summary>
/// 网格渲染器：在指定矩形区域内绘制瓦片网格线。
/// </summary>
internal static class GridRenderer
{
    /// <summary>
    /// 在 SpriteBatch 上绘制网格
    /// </summary>
    /// <param name="spriteBatch">绘图上下文</param>
    /// <param name="width">区域总宽度（像素）</param>
    /// <param name="height">区域总高度（像素）</param>
    /// <param name="tileSize">单个瓦片尺寸（像素），默认 64</param>
    /// <param name="color">网格颜色</param>
    /// <param name="thickness">线条粗细（像素）</param>
    public static void Draw(
        SpriteBatch spriteBatch,
        int width,
        int height,
        int tileSize = Game1.tileSize,
        Color? color = null,
        int thickness = 1)
    {
        var gridColor = color ?? Color.Black;
        var pixel = GetWhitePixel(spriteBatch.GraphicsDevice);

        // 垂直线
        for (int x = 0; x <= width; x += tileSize)
        {
            spriteBatch.Draw(
                pixel,
                new Rectangle(x, 0, thickness, height),
                gridColor);
        }

        // 水平线
        for (int y = 0; y <= height; y += tileSize)
        {
            spriteBatch.Draw(
                pixel,
                new Rectangle(0, y, width, thickness),
                gridColor);
        }
    }

    /// <summary>
    /// 获取 1x1 白色像素纹理（用于绘制线条）
    /// </summary>
    private static Texture2D GetWhitePixel(GraphicsDevice device)
    {
        var texture = new Texture2D(device, 1, 1);
        texture.SetData(new[] { Color.White });
        return texture;
    }
}
