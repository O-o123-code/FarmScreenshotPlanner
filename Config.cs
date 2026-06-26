using StardewModdingAPI;

namespace FarmBlueprint;

/// <summary>
/// 模组配置模型，所有选项支持 GMCM 热更新。
/// </summary>
internal class ModConfig
{
    /* ===== 快捷键 ===== */

    /// <summary>截图触发按键（默认 P）</summary>
    public SButton ScreenshotKey { get; set; } = SButton.P;

    /* ===== 截图区域 ===== */

    /// <summary>截图区域选择：Farm / Greenhouse / Island / CurrentLocation</summary>
    public CaptureArea CaptureArea { get; set; } = CaptureArea.Farm;

    /* ===== 网格样式 ===== */

    /// <summary>是否启用网格</summary>
    public bool EnableGrid { get; set; } = true;

    /// <summary>网格颜色（RGB 十六进制，默认 #FF000000 黑色）</summary>
    public string GridColor { get; set; } = "#FF000000";

    /// <summary>网格线条粗细（像素，1~3）</summary>
    public int GridThickness { get; set; } = 1;

    /// <summary>网格透明度（0~255）</summary>
    public int GridOpacity { get; set; } = 128;

    /// <summary>是否仅在截图时渲染网格（平时隐藏）</summary>
    public bool GridOnlyOnScreenshot { get; set; } = true;

    /* ===== 保存设置 ===== */

    /// <summary>截图后自动打开保存文件夹</summary>
    public bool AutoOpenFolder { get; set; } = false;

    /// <summary>文件名前缀</summary>
    public string FileNamePrefix { get; set; } = "farm_blueprint";
}

/// <summary>
/// 截图区域枚举
/// </summary>
internal enum CaptureArea
{
    Farm,
    Greenhouse,
    Island,
    CurrentLocation
}
