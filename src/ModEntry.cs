using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace FarmScreenshotPlanner;

public class ModEntry : Mod
{
    internal ModConfig Config { get; private set; } = null!;
    internal RollingFileLogger LogFile { get; private set; } = null!;
    internal LocationService LocationService { get; private set; } = null!;
    internal ScreenshotOrchestrator Orchestrator { get; private set; } = null!;
    internal ConfigMenu ConfigMenu { get; private set; } = null!;

    public override void Entry(IModHelper helper)
    {
        LogFile = new RollingFileLogger(helper.DirectoryPath);
        LogFile.Info("Mod initializing...");

        Config = helper.ReadConfig<ModConfig>();

        helper.Events.Input.ButtonsChanged += OnButtonsChanged;
        helper.ConsoleCommands.Add("farm_screenshot", "Capture a full-resolution farm screenshot.\n\nUsage: farm_screenshot [location_name]\nIf no location is specified, uses the configured region.", OnFarmScreenshotCommand);

        LocationService = new();
        Orchestrator = new(this);
        ConfigMenu = new(this);

        ConfigMenu.TryRegister(helper);

        LogFile.Info("Mod initialized.");
    }

    private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
    {
        if (!Context.IsWorldReady) return;
        if (e.Pressed.Contains(Config.Hotkey))
        {
            Orchestrator.ExecuteCapture();
        }
    }

    private void OnFarmScreenshotCommand(string command, string[] args)
    {
        if (!Context.IsWorldReady)
        {
            LogFile.Warn("Command ignored: no save loaded.");
            Monitor.Log("No save loaded. Please load a save first.", LogLevel.Warn);
            return;
        }

        string? locationName = args.Length > 0 ? string.Join(" ", args) : null;
        Orchestrator.ExecuteCapture(locationName);
    }
}
