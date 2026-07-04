using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace FarmScreenshotPlanner;

public interface IGenericModConfigMenuApi
{
    void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
    void Unregister(IManifest mod);
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
    private IGenericModConfigMenuApi? _api;
    private IModHelper? _helper;
    
    // 缓存位置列表，避免每次重建菜单时重新获取
    private string[]? _cachedLocationOptions;
    private int _cachedLocationCount;
    private LocalizedContentManager.LanguageCode _cachedLanguage;

    public ConfigMenu(ModEntry mod)
    {
        _mod = mod;
        _config = mod.Config;
    }

    public void TryRegister(IModHelper helper)
    {
        if (!helper.ModRegistry.IsLoaded("spacechase0.GenericModConfigMenu"))
        {
            _mod.Monitor.Info(helper.Translation.Get("log.gmcm_not_found"));
            return;
        }

        _api = helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(
            "spacechase0.GenericModConfigMenu");
        if (_api is null) return;
        _helper = helper;

        helper.Events.GameLoop.SaveLoaded += OnSaveChanged;
        helper.Events.GameLoop.ReturnedToTitle += OnSaveChanged;
        helper.Events.Player.Warped += OnWarped;

        RebuildMenu();
    }

    private void OnSaveChanged(object? sender, EventArgs e)
    {
        _cachedLocationOptions = null;
        RebuildMenu();
    }

    private void OnWarped(object? sender, StardewModdingAPI.Events.WarpedEventArgs e)
    {
        // Invalidate cache and rebuild menu when player warps (may have entered/exited a building)
        _cachedLocationOptions = null;
        RebuildMenu();
    }

    private void RebuildMenu()
    {
        if (_api is null || _helper is null) return;

        _api.Unregister(_mod.ModManifest);
        _api.Register(_mod.ModManifest, Reset, Save);

        if (!Context.IsWorldReady)
        {
            _api.AddParagraph(_mod.ModManifest,
                () => _helper.Translation.Get("gmcm.not_ready"));
            return;
        }

        var manifest = _mod.ModManifest;

        _api.AddSectionTitle(manifest, () => _helper.Translation.Get("gmcm.hotkey"));

        _api.AddKeybindList(manifest,
            () => _config.Hotkey,
            val => _config.Hotkey = val,
            () => _helper.Translation.Get("gmcm.hotkey"),
            tooltip: () => _helper.Translation.Get("gmcm.hotkey_tooltip"));

        _api.AddKeybindList(manifest,
            () => _config.CancelHotkey,
            val => _config.CancelHotkey = val,
            () => _helper.Translation.Get("gmcm.cancel_hotkey"),
            tooltip: () => _helper.Translation.Get("gmcm.cancel_hotkey_tooltip"));

        var locations = GetCachedLocationOptions();

        _api.AddTextOption(manifest,
            () => _config.SelectedLocation,
            val => _config.SelectedLocation = val,
            () => _helper.Translation.Get("gmcm.location"),
            allowedValues: locations);

        string[] scaleChoices = { "25%", "50%", "75%", "100%" };
        _api.AddTextOption(manifest,
            () => (int)(_config.OutputScale * 100) + "%",
            val =>
            {
                if (int.TryParse(val.Replace("%", ""), out int pct))
                    _config.OutputScale = pct / 100f;
            },
            () => _helper.Translation.Get("gmcm.scale"),
            tooltip: () => _helper.Translation.Get("gmcm.scale_tooltip"),
            allowedValues: scaleChoices);

        string[] formatChoices = { "PNG", "JPEG" };
        _api.AddTextOption(manifest,
            () => _config.OutputFormat.ToString(),
            val => _config.OutputFormat = Enum.Parse<OutputFormat>(val),
            () => _helper.Translation.Get("gmcm.output_format"),
            tooltip: () => _helper.Translation.Get("gmcm.output_format_tooltip"),
            allowedValues: formatChoices);

        _api.AddNumberOption(manifest,
            () => _config.JpegQuality,
            val => _config.JpegQuality = val,
            () => _helper.Translation.Get("gmcm.jpeg_quality"),
            tooltip: () => _helper.Translation.Get("gmcm.jpeg_quality_tooltip"),
            min: 50, max: 100, interval: 5,
            formatValue: v => v + "%");

        _api.AddSectionTitle(manifest, () => _helper.Translation.Get("gmcm.grid_enabled"));
        _api.AddBoolOption(manifest,
            () => _config.Grid.Enabled,
            val => _config.Grid.Enabled = val,
            () => _helper.Translation.Get("gmcm.grid_enabled"));

        string[] colorPresets = {
            "00000060", "FFFFFF60", "FF000060", "0000FF60",
            "00FF0060", "00FFFF60", "FF00FF60", "FFA50060", "80008060"
        };
        _api.AddTextOption(manifest,
            () => _config.Grid.Color,
            val => _config.Grid.Color = val,
            () => _helper.Translation.Get("gmcm.grid_color"),
            allowedValues: colorPresets,
            formatAllowedValue: val =>
            {
                string key = "grid.color." + val;
                string t = _helper.Translation.Get(key);
                return t != key ? t : val;
            });

        _api.AddNumberOption(manifest,
            () => _config.Grid.Thickness,
            val => _config.Grid.Thickness = val,
            () => _helper.Translation.Get("gmcm.grid_thickness"),
            min: 1, max: 3, interval: 1);

        _api.AddNumberOption(manifest,
            () => _config.Grid.Opacity,
            val => _config.Grid.Opacity = val,
            () => _helper.Translation.Get("gmcm.grid_opacity"),
            min: 0f, max: 1f, interval: 0.05f);

        _api.AddBoolOption(manifest,
            () => _config.UseGameScreenshotFolder,
            val => _config.UseGameScreenshotFolder = val,
            () => _helper.Translation.Get("gmcm.use_game_screenshot_folder"),
            tooltip: () => _helper.Translation.Get("gmcm.use_game_screenshot_folder_tooltip"));

        _api.AddParagraph(manifest, () =>
        {
            string savePath = _config.UseGameScreenshotFolder
                ? Game1.game1.GetScreenshotFolder(false)
                : (string.IsNullOrEmpty(_config.SavePath)
                    ? Path.Combine(_mod.Helper.DirectoryPath, "Screenshots")
                    : _config.SavePath);
            return _helper.Translation.Get("gmcm.save_path_label") + ": " + savePath;
        });

        _api.AddBoolOption(manifest,
            () => _config.DeleteGameOriginal,
            val => _config.DeleteGameOriginal = val,
            () => _helper.Translation.Get("gmcm.delete_original"),
            tooltip: () => _helper.Translation.Get("gmcm.delete_original_tooltip"));

        _api.AddParagraph(manifest, () =>
            _helper.Translation.Get("gmcm.game_screenshot_path") + ": " + Game1.game1.GetScreenshotFolder(false));
    }

    private void Reset()
    {
        var defaults = new ModConfig();
        _config.Hotkey = defaults.Hotkey;
        _config.CancelHotkey = defaults.CancelHotkey;
        _config.SelectedLocation = defaults.SelectedLocation;
        _config.OutputScale = defaults.OutputScale;
        _config.OutputFormat = defaults.OutputFormat;
        _config.JpegQuality = defaults.JpegQuality;
        _config.SavePath = defaults.SavePath;
        _config.UseGameScreenshotFolder = defaults.UseGameScreenshotFolder;
        _config.DeleteGameOriginal = defaults.DeleteGameOriginal;
        _config.Grid.Enabled = defaults.Grid.Enabled;
        _config.Grid.Color = defaults.Grid.Color;
        _config.Grid.Thickness = defaults.Grid.Thickness;
        _config.Grid.Opacity = defaults.Grid.Opacity;
    }

    private void Save()
    {
        _config.Validate();
        _mod.Helper.WriteConfig(_config);
    }

    private string[] GetCachedLocationOptions()
    {
        if (_helper is null) return Array.Empty<string>();

        var lang = Game1.content.GetCurrentLanguage();
        int locationCount = _mod.LocationService.GetLocations().Count();

        // 如果缓存有效（位置数量和语言未变化），直接返回缓存
        if (_cachedLocationOptions is not null
            && _cachedLocationCount == locationCount
            && _cachedLanguage == lang)
        {
            return _cachedLocationOptions;
        }

        // 重新构建位置列表（使用分组标题，按显示名称排序）
        _cachedLocationOptions = _mod.LocationService.GetLocations()
            .Select(loc => _mod.LocationService.GetGroupedDisplayTitle(loc))
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .Prepend(_helper.Translation.Get("config.current_location"))
            .ToArray();
        _cachedLocationCount = locationCount;
        _cachedLanguage = lang;

        return _cachedLocationOptions;
    }
}
