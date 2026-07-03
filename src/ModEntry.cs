using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace FarmScreenshotPlanner;

public class ModEntry : Mod
{
    internal ModConfig Config { get; private set; } = null!;
    internal LocationService LocationService { get; private set; } = null!;
    internal ScreenshotOrchestrator Orchestrator { get; private set; } = null!;
    internal ConfigMenu ConfigMenu { get; private set; } = null!;

    public override void Entry(IModHelper helper)
    {
        Monitor.Info("Mod initializing...");

        Config = helper.ReadConfig<ModConfig>();
        Config.Validate();

        helper.Events.Input.ButtonPressed += OnButtonPressed;
        helper.ConsoleCommands.Add("farm_screenshot", "Capture a full-resolution farm screenshot.\n\nUsage: farm_screenshot [location_name]\nIf no location is specified, uses the configured region.", OnFarmScreenshotCommand);
        helper.ConsoleCommands.Add("farm_screenshot_cancel", "Cancel an in-progress screenshot capture.", OnFarmScreenshotCancelCommand);

        LocationService = new();
        Orchestrator = new(this);
        ConfigMenu = new(this);

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;

        Monitor.Info("Mod initialized.");
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        ConfigMenu.TryRegister(Helper);
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady) return;

        if (Config.Hotkey.JustPressed())
        {
            Orchestrator.ExecuteCapture(Config.SelectedLocation);
        }
        else if (Config.CancelHotkey.JustPressed() && Orchestrator.IsRendering)
        {
            Orchestrator.CancelCapture();
        }
    }

    private void OnFarmScreenshotCommand(string command, string[] args)
    {
        if (!Context.IsWorldReady)
        {
            Monitor.Warn("Command ignored: no save loaded.");
            return;
        }

        string? locationName = args.Length > 0 ? string.Join(" ", args) : null;
        Orchestrator.ExecuteCapture(locationName);
    }

    private void OnFarmScreenshotCancelCommand(string command, string[] args)
    {
        if (!Orchestrator.IsRendering)
        {
            Monitor.Info("No screenshot in progress.");
            return;
        }

        Orchestrator.CancelCapture();
        Monitor.Info("Screenshot cancelled.");
    }
}
