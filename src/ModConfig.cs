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
}

public class GridConfig
{
    public bool Enabled { get; set; } = true;
    public string Color { get; set; } = "00000040";
    public int Thickness { get; set; } = 1;
    public float Opacity { get; set; } = 0.5f;
}
