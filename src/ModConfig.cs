using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace FarmScreenshotPlanner;

public class ModConfig
{
    public KeybindList Hotkey { get; set; } = new(SButton.J);
    public string SelectedLocation { get; set; } = "Current Location";
    public float OutputScale { get; set; } = 0.25f;
    public string SavePath { get; set; } = string.Empty;
    public bool DeleteGameOriginal { get; set; } = true;
    public GridConfig Grid { get; set; } = new();

    public void Validate()
    {
        // 限制 OutputScale 在合理范围内
        OutputScale = Math.Clamp(OutputScale, 0.1f, 1.0f);

        // 验证 Grid 配置
        Grid.Validate();
    }
}

public class GridConfig
{
    public bool Enabled { get; set; } = true;
    public string Color { get; set; } = "00000040";
    public int Thickness { get; set; } = 1;
    public float Opacity { get; set; } = 0.5f;

    public void Validate()
    {
        // 限制 Thickness 在合理范围内
        Thickness = Math.Clamp(Thickness, 1, 10);

        // 限制 Opacity 在 0-1 范围内
        Opacity = Math.Clamp(Opacity, 0f, 1f);

        // 验证 Color 格式（应为 8 位十六进制）
        if (string.IsNullOrEmpty(Color) || Color.Length != 8 || !IsValidHex(Color))
        {
            Color = "00000040";
        }
    }

    private static bool IsValidHex(string s)
    {
        foreach (char c in s)
        {
            if (!Uri.IsHexDigit(c)) return false;
        }
        return true;
    }
}
