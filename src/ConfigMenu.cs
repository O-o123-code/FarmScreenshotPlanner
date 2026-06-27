using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;

namespace FarmScreenshotPlanner;

public interface IGenericModConfigMenuApi
{
    void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
    void AddSectionTitle(IManifest mod, Func<string> text, Func<string>? tooltip = null);
    void AddKeybindList(IManifest mod, Func<KeybindList> getValue, Action<KeybindList> setValue, Func<string> name, Func<string>? tooltip = null, string? fieldId = null);
    void AddTextOption(IManifest mod, Func<string> getValue, Action<string> setValue, Func<string> name, Func<string>? tooltip = null, string[]? allowedValues = null, Func<string, string>? formatAllowedValue = null, string? fieldId = null);
    void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string>? tooltip = null, string? fieldId = null);
    void AddNumberOption(IManifest mod, Func<int> getValue, Action<int> setValue, Func<string> name, Func<string>? tooltip = null, int? min = null, int? max = null, int? interval = null, Func<int, string>? formatValue = null, string? fieldId = null);
    void AddNumberOption(IManifest mod, Func<float> getValue, Action<float> setValue, Func<string> name, Func<string>? tooltip = null, float? min = null, float? max = null, float? interval = null, Func<float, string>? formatValue = null, string? fieldId = null);
    void AddParagraph(IManifest mod, Func<string> text);
}

public class ConfigMenu
{
    private readonly ModEntry _mod;
    private readonly ModConfig _config;
    private IModHelper? _helper;
    private IGenericModConfigMenuApi? _api;
    private bool _fullRegistered;

    public ConfigMenu(ModEntry mod)
    {
        _mod = mod;
        _config = mod.Config;
    }

    public void TryRegister(IModHelper helper)
    {
        if (!helper.ModRegistry.IsLoaded("spacechase0.GenericModConfigMenu"))
        {
            _mod.LogFile.Info(helper.Translation.Get("log.gmcm_not_found"));
            return;
        }

        _helper = helper;
        _api = helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(
            "spacechase0.GenericModConfigMenu");
        if (_api is null) return;

        RegisterMenu(includeLocations: false);

        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        if (_api is null || _helper is null) return;
        if (_fullRegistered) return;
        _fullRegistered = true;

        RegisterMenu(includeLocations: true);
    }

    private void RegisterMenu(bool includeLocations)
    {
        var api = _api!;
        var helper = _helper!;
        var manifest = _mod.ModManifest;

        api.Register(manifest, Reset, Save);

        api.AddSectionTitle(manifest, () => helper.Translation.Get("gmcm.hotkey"));

        api.AddKeybindList(manifest,
            () => _config.Hotkey,
            val => _config.Hotkey = val,
            () => helper.Translation.Get("gmcm.hotkey"));

        if (includeLocations)
        {
            var locNames = new List<string> { "Current Location" };
            foreach (var loc in _mod.LocationService.GetLocations())
            {
                string title = _mod.LocationService.GetDisplayTitle(loc);
                if (!locNames.Contains(title))
                    locNames.Add(title);
            }

            api.AddTextOption(manifest,
                () => _config.SelectedLocation,
                val => _config.SelectedLocation = val,
                () => helper.Translation.Get("gmcm.location"),
                allowedValues: locNames.ToArray(),
                formatAllowedValue: val =>
                    val == "Current Location"
                        ? helper.Translation.Get("config.current_location")
                        : val);
        }
        else
        {
            api.AddTextOption(manifest,
                () => _config.SelectedLocation,
                val => _config.SelectedLocation = val,
                () => helper.Translation.Get("gmcm.location"));
        }

        string[] scaleChoices = { "25%", "50%", "75%", "100%" };
        api.AddTextOption(manifest,
            () => (int)(_config.OutputScale * 100) + "%",
            val =>
            {
                if (int.TryParse(val.Replace("%", ""), out int pct))
                    _config.OutputScale = pct / 100f;
            },
            () => helper.Translation.Get("gmcm.scale"),
            tooltip: () => helper.Translation.Get("gmcm.scale_tooltip"),
            allowedValues: scaleChoices);

        api.AddSectionTitle(manifest, () => helper.Translation.Get("gmcm.grid_enabled"));
        api.AddBoolOption(manifest,
            () => _config.Grid.Enabled,
            val => _config.Grid.Enabled = val,
            () => helper.Translation.Get("gmcm.grid_enabled"));

        string[] colorPresets = {
            "00000060", "FFFFFF60", "FF000060", "0000FF60",
            "00FF0060", "00FFFF60", "FF00FF60", "FFA50060", "80008060"
        };
        api.AddTextOption(manifest,
            () => _config.Grid.Color,
            val => _config.Grid.Color = val,
            () => helper.Translation.Get("gmcm.grid_color"),
            allowedValues: colorPresets,
            formatAllowedValue: val =>
            {
                string key = "grid.color." + val;
                string t = helper.Translation.Get(key);
                return t != key ? t : val;
            });

        api.AddNumberOption(manifest,
            () => _config.Grid.Thickness,
            val => _config.Grid.Thickness = val,
            () => helper.Translation.Get("gmcm.grid_thickness"),
            min: 1, max: 3, interval: 1);

        api.AddNumberOption(manifest,
            () => _config.Grid.Opacity,
            val => _config.Grid.Opacity = val,
            () => helper.Translation.Get("gmcm.grid_opacity"),
            min: 0f, max: 1f, interval: 0.05f);

        string savePathDisplay = string.IsNullOrEmpty(_config.SavePath)
            ? Path.Combine(_mod.Helper.DirectoryPath, "Screenshots")
            : _config.SavePath;
        api.AddParagraph(manifest, () =>
            helper.Translation.Get("gmcm.save_path_label") + ": " + savePathDisplay);
    }

    private void Reset()
    {
        var defaults = new ModConfig();
        _config.Hotkey = defaults.Hotkey;
        _config.SelectedLocation = defaults.SelectedLocation;
        _config.OutputScale = defaults.OutputScale;
        _config.SavePath = defaults.SavePath;
        _config.Grid.Enabled = defaults.Grid.Enabled;
        _config.Grid.Color = defaults.Grid.Color;
        _config.Grid.Thickness = defaults.Grid.Thickness;
        _config.Grid.Opacity = defaults.Grid.Opacity;
    }

    private void Save()
    {
        _mod.Helper.WriteConfig(_config);
    }
}
