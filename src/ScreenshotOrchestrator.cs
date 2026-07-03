using System.Reflection;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace FarmScreenshotPlanner;

public class ScreenshotOrchestrator
{
    private readonly ModEntry _mod;
    private readonly TileGridRenderer _gridRenderer = new();
    private readonly ScreenshotSaver _saver = new();
    private readonly TimeStateFreezer _freezer = new();
    private readonly HUDMessageProxy _hud;

    private bool _isRendering;
    private string? _screenshotFolder;
    private HashSet<string> _existingFiles = new(StringComparer.OrdinalIgnoreCase);
    private GameLocation? _pendingLocation;
    private string? _pendingPrefix;
    private int _waitTicks;

    private MethodInfo? _cachedMethodWithLocation;
    private MethodInfo? _cachedMethodPublic;

    private const int SafetyLimit = 16384;
    private const int TimeoutTicks = 600; // ~10 seconds at 60 ticks/sec
    private const int PollInterval = 5;   // check every 5 ticks

    public bool IsRendering => _isRendering;

    public ScreenshotOrchestrator(ModEntry mod)
    {
        _mod = mod;
        _hud = new HUDMessageProxy(mod.LogFile);
    }

    public void ExecuteCapture(string? locationName = null)
    {
        if (_isRendering)
        {
            _mod.LogFile.Warn("Screenshot already in progress, ignoring duplicate trigger.");
            return;
        }

        if (Game1.activeClickableMenu is not null)
        {
            _hud.Show(_mod.Helper.Translation.Get("hud.menu_open"));
            _mod.LogFile.Info("Screenshot aborted: a menu is open.");
            return;
        }

        _isRendering = true;
        try
        {
            _mod.LogFile.Debug($"ExecuteCapture triggered, locationName={(locationName ?? "null")}");

            var location = ResolveLocation(locationName);
            if (location is null)
            {
                _mod.LogFile.Warn("No valid location found for screenshot.");
                _isRendering = false;
                return;
            }

            _mod.LogFile.Debug($"Resolved location: {location.Name ?? "null"}");

            _freezer.Freeze();
            _hud.Show(string.Format(_mod.Helper.Translation.Get("hud.rendering"), _mod.Config.CancelHotkey));

            // Record existing screenshots so we can detect the new file by elimination
            _screenshotFolder = Game1.game1.GetScreenshotFolder(true);
            _existingFiles = Directory.Exists(_screenshotFolder)
                ? new HashSet<string>(Directory.GetFiles(_screenshotFolder), StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            _pendingLocation = location;
            _pendingPrefix = _mod.LocationService.GetDisplayTitle(location);
            _waitTicks = 0;

            // Invoke the game's built-in map screenshot (full scale for maximum quality)
            string? result = InvokeGameScreenshot(location, 1f, _pendingPrefix);
            _mod.LogFile.Debug($"takeMapScreenshot returned: {result ?? "(null)"}");

            // Start async polling for the new screenshot file
            _mod.Helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }
        catch (Exception ex)
        {
            _mod.LogFile.Error($"ExecuteCapture failed: {ex.Message}");
            _mod.Monitor.Log($"Screenshot failed: {ex}", LogLevel.Error);
            Cleanup(false);
        }
    }

    public void CancelCapture()
    {
        if (!_isRendering) return;
        
        _mod.LogFile.Info("Screenshot cancelled by player (Escape pressed).");
        _hud.Hide();
        _hud.Show(_mod.Helper.Translation.Get("hud.cancelled"));
        Cleanup(false);
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        // Safety: abort if a menu was opened during the async wait
        if (Game1.activeClickableMenu is not null)
        {
            _mod.LogFile.Info("Screenshot aborted: menu opened during async wait.");
            Cleanup(false);
            return;
        }

        _waitTicks++;

        if (_waitTicks % PollInterval != 0)
            return;

        string? newFile = FindNewScreenshotFile();

        if (newFile is null)
        {
            if (_waitTicks >= TimeoutTicks)
            {
                _mod.LogFile.Warn("Screenshot timed out waiting for file.");
                _hud.Hide();
                Cleanup(false);
                _hud.Show(_mod.Helper.Translation.Get("error.timeout"));
            }
            return;
        }

        // File exists but may still be written by the game — wait until it's unlocked
        if (!FileIsReady(newFile))
        {
            _mod.LogFile.Debug($"File still locked, retrying: {newFile}");
            if (_waitTicks >= TimeoutTicks)
            {
                _mod.LogFile.Warn("Screenshot timed out waiting for file to be unlocked.");
                _hud.Hide();
                Cleanup(false);
                _hud.Show(_mod.Helper.Translation.Get("error.timeout"));
            }
            return;
        }

        _mod.LogFile.Info($"Game screenshot captured: {newFile}");

        try
        {
            ProcessScreenshot(newFile);
            Cleanup(true);
        }
        catch (Exception ex)
        {
            _mod.LogFile.Error($"ProcessScreenshot failed: {ex.Message}");
            _mod.Monitor.Log($"Screenshot processing failed: {ex}", LogLevel.Error);

            Cleanup(false);

            if (ex is UnauthorizedAccessException or IOException)
                _hud.Show(_mod.Helper.Translation.Get("error.disk_full"));
            else
                _hud.Show(string.Format(_mod.Helper.Translation.Get("log.screenshot_failed"), ex.Message));
        }
    }

    /// <summary>
    /// Loads the game's screenshot PNG, applies grid overlay, saves to mod output folder.
    /// </summary>
    private void ProcessScreenshot(string screenshotPath)
    {
        var gd = Game1.graphics.GraphicsDevice;

        // Load the PNG file as a GPU texture
        using var stream = File.OpenRead(screenshotPath);
        using var sourceTexture = Texture2D.FromStream(gd, stream);

        _mod.LogFile.Debug($"Loaded screenshot: {sourceTexture.Width}x{sourceTexture.Height}");

        // Calculate output dimensions based on configured scale
        float scale = _mod.Config.OutputScale;
        int finalW = Math.Max(1, (int)(sourceTexture.Width * scale));
        int finalH = Math.Max(1, (int)(sourceTexture.Height * scale));

        if (finalW > SafetyLimit || finalH > SafetyLimit)
        {
            float ratio = Math.Min(SafetyLimit / (float)finalW, SafetyLimit / (float)finalH);
            finalW = Math.Max(1, (int)(finalW * ratio));
            finalH = Math.Max(1, (int)(finalH * ratio));
            _mod.LogFile.Warn($"Size exceeds safety limit, scaled to {finalW}x{finalH}");
        }

        _mod.LogFile.Debug($"Final output size: {finalW}x{finalH}");

        var finalRT = _gridRenderer.Apply(sourceTexture, _mod.Config, finalW, finalH);
        try
        {
            string saveDir = ResolveSaveDirectory();
            _mod.LogFile.Debug($"Saving to directory: {saveDir}, prefix: {_pendingPrefix}");
            string savePath = _saver.Save(finalRT, saveDir, _pendingPrefix!);

            if (_mod.Config.DeleteGameOriginal)
            {
                try
                {
                    File.Delete(screenshotPath);
                    _mod.LogFile.Debug($"Deleted game original: {screenshotPath}");
                }
                catch (Exception ex)
                {
                    _mod.LogFile.Warn($"Failed to delete game original: {ex.Message}");
                }
            }

            _hud.Hide();
            _hud.Show(_mod.Helper.Translation.Get("hud.saved_brief"));

            _mod.LogFile.Info($"Screenshot saved: {savePath}");
            _mod.Monitor.Log($"Screenshot saved: {savePath}", LogLevel.Info);

            Game1.activeClickableMenu = new ScreenshotResultMenu(savePath, _pendingPrefix!, _mod);
        }
        finally
        {
            finalRT.Dispose();
        }
    }

    /// <summary>
    /// Scans the screenshot folder for a new file that didn't exist before the capture.
    /// </summary>
    private string? FindNewScreenshotFile()
    {
        if (_screenshotFolder is null || !Directory.Exists(_screenshotFolder))
            return null;

        foreach (var file in Directory.GetFiles(_screenshotFolder))
        {
            if (!_existingFiles.Contains(file))
                return file;
        }
        return null;
    }

    /// <summary>
    /// Returns true if the file exists and is not locked by another process.
    /// </summary>
    private static bool FileIsReady(string path)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// Invokes Game1.takeMapScreenshot via reflection.
    /// Tries the private overload (with GameLocation) first, then falls back to the public one.
    /// For the public overload, temporarily swaps Game1.currentLocation to the target location
    /// so that any location can be captured regardless of where the player currently is.
    /// </summary>
    private string? InvokeGameScreenshot(GameLocation location, float scale, string name)
    {
        var game1 = Game1.game1;
        var game1Type = game1.GetType();
        var onDone = new Action(() => _mod.LogFile.Debug("Game screenshot onDone callback fired."));

        // Attempt 1: private overload with GameLocation parameter
        //   private String takeMapScreenshot(GameLocation, Single, String, Action)
        _cachedMethodWithLocation ??= FindMethod(game1Type, "takeMapScreenshot",
            typeof(GameLocation), typeof(float), typeof(string), typeof(Action));
        if (_cachedMethodWithLocation is not null)
        {
            _mod.LogFile.Debug("Calling takeMapScreenshot(GameLocation, float, string, Action)");
            return _cachedMethodWithLocation.Invoke(game1, new object[] { location, scale, name, onDone }) as string;
        }

        // Attempt 2: public overload without GameLocation
        //   public String takeMapScreenshot(Nullable<Single>, String, Action)
        _cachedMethodPublic ??= FindMethod(game1Type, "takeMapScreenshot",
            typeof(float?), typeof(string), typeof(Action));
        if (_cachedMethodPublic is not null)
        {
            _mod.LogFile.Debug("Calling takeMapScreenshot(float?, string, Action) with location swap");

            // Temporarily swap Game1.currentLocation to the target location
            var originalLocation = Game1.currentLocation;
            try
            {
                Game1.currentLocation = location;
                return _cachedMethodPublic.Invoke(game1, new object?[] { (float?)scale, name, onDone }) as string;
            }
            finally
            {
                Game1.currentLocation = originalLocation;
            }
        }

        _mod.LogFile.Warn("takeMapScreenshot method not found in this game version.");
        _hud.Hide();
        _hud.Show(_mod.Helper.Translation.Get("error.game_screenshot_unavailable"));
        return null;
    }

    /// <summary>
    /// Finds a method on a type by name and exact parameter types.
    /// </summary>
    private static MethodInfo? FindMethod(Type type, string name, params Type[] paramTypes)
    {
        foreach (var method in type.GetMethods(
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static))
        {
            if (method.Name != name) continue;
            var parms = method.GetParameters();
            if (parms.Length != paramTypes.Length) continue;
            bool match = true;
            for (int i = 0; i < parms.Length; i++)
            {
                if (parms[i].ParameterType != paramTypes[i])
                {
                    match = false;
                    break;
                }
            }
            if (match) return method;
        }
        return null;
    }

    private GameLocation? ResolveLocation(string? locationName)
    {
        string internalMarker = _mod.Helper.Translation.Get("config.current_location");
        if (string.IsNullOrEmpty(locationName) || locationName == "Current Location" || locationName == internalMarker)
            return Game1.currentLocation;

        foreach (var loc in _mod.LocationService.GetLocations())
        {
            string display = _mod.LocationService.GetDisplayTitle(loc);
            if (display.Equals(locationName, StringComparison.OrdinalIgnoreCase) ||
                loc.Name?.Equals(locationName, StringComparison.OrdinalIgnoreCase) == true)
            {
                return loc;
            }
        }

        return null;
    }

    private string ResolveSaveDirectory()
    {
        if (_mod.Config.UseGameScreenshotFolder)
        {
            return Game1.game1.GetScreenshotFolder(true);
        }
        if (!string.IsNullOrWhiteSpace(_mod.Config.SavePath))
        {
            return _mod.Config.SavePath;
        }
        return Path.Combine(_mod.Helper.DirectoryPath, "Screenshots");
    }

    /// <summary>
    /// Unsubscribes from polling, restores game state, optionally hides HUD.
    /// </summary>
    private void Cleanup(bool success)
    {
        _mod.Helper.Events.GameLoop.UpdateTicked -= OnUpdateTicked;
        _freezer.Restore();
        if (!success)
            _hud.Hide();
        _isRendering = false;
        _pendingLocation = null;
        _pendingPrefix = null;
    }
}
