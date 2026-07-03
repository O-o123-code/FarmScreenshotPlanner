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
    private GameLocation? _pendingLocation;
    private string? _pendingPrefix;
    private int _waitTicks;
    private DateTime _captureStartTime;  // 截图开始时间，用于区分新旧文件
    private GameLocation? _originalLocation;  // 保存原始位置，用于延迟恢复

    private MethodInfo? _cachedMethodWithLocation;
    private MethodInfo? _cachedMethodPublic;

    private const int SafetyLimit = 16384;
    private const int TimeoutTicks = 600; // ~10 seconds at 60 ticks/sec
    private const int PollInterval = 5;   // check every 5 ticks

    public bool IsRendering => _isRendering;

    public ScreenshotOrchestrator(ModEntry mod)
    {
        _mod = mod;
        _hud = new HUDMessageProxy(mod.Monitor);
    }

    public void ExecuteCapture(string? locationName = null)
    {
        if (_isRendering)
        {
            _mod.Monitor.Warn("Screenshot already in progress, ignoring duplicate trigger.");
            return;
        }

        if (Game1.activeClickableMenu is not null)
        {
            _hud.Show(_mod.Helper.Translation.Get("hud.menu_open"));
            _mod.Monitor.Info("Screenshot aborted: a menu is open.");
            return;
        }

        _isRendering = true;
        try
        {
            _mod.Monitor.Debug($"ExecuteCapture triggered, locationName={(locationName ?? "null")}");

            var location = ResolveLocation(locationName);
            if (location is null)
            {
                _mod.Monitor.Warn("No valid location found for screenshot.");
                _isRendering = false;
                return;
            }

            _mod.Monitor.Debug($"Resolved location: {location.Name ?? "null"}");

            _freezer.Freeze();
            _hud.Show(string.Format(_mod.Helper.Translation.Get("hud.rendering"), _mod.Config.CancelHotkey));

            _screenshotFolder = Game1.game1.GetScreenshotFolder(true);
            _pendingLocation = location;
            _pendingPrefix = location.Name ?? "Unknown";
            _waitTicks = 0;
            _captureStartTime = DateTime.Now;  // 记录截图开始时间

            // 清理同名旧文件，避免轮询时匹配到旧截图
            TryCleanupExistingScreenshot(_screenshotFolder, _pendingPrefix);

            // Invoke the game's built-in map screenshot (full scale for maximum quality)
            _mod.Monitor.Debug($"Invoking takeMapScreenshot for location: {location.Name}");
            string? result = InvokeGameScreenshot(location, 1f, _pendingPrefix);
            _mod.Monitor.Debug($"takeMapScreenshot returned: {result ?? "(null)"}");

            // Start async polling for the screenshot file
            _mod.Helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }
        catch (Exception ex)
        {
            _mod.Monitor.Error($"ExecuteCapture failed: {ex.Message}");
            Cleanup(false);
        }
    }

    public void CancelCapture()
    {
        if (!_isRendering) return;
        
        _mod.Monitor.Info($"Screenshot cancelled by player (CancelHotkey: {_mod.Config.CancelHotkey}).");
        _hud.Hide();
        _hud.Show(_mod.Helper.Translation.Get("hud.cancelled"));
        Cleanup(false);
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        // Safety: abort if a menu was opened during the async wait
        if (Game1.activeClickableMenu is not null)
        {
            _mod.Monitor.Info("Screenshot aborted: menu opened during async wait.");
            Cleanup(false);
            return;
        }

        _waitTicks++;

        // 进度反馈：每秒更新 HUD 显示
        if (_waitTicks % 60 == 0)
        {
            float seconds = _waitTicks / 60f;
            string baseMsg = string.Format(_mod.Helper.Translation.Get("hud.rendering"), _mod.Config.CancelHotkey);
            _hud.Hide();
            _hud.Show($"{baseMsg} ({seconds:F0}s)");
        }

        if (_waitTicks % PollInterval != 0)
            return;

        // 查找以 prefix 开头的最新截图文件
        // 游戏内置截图命名格式：{prefix}_{日期}_{时间戳}.png
        string? latestFile = FindLatestScreenshotFile(_pendingPrefix!);
        
        if (latestFile is null)
        {
            _mod.Monitor.Debug($"Poll #{_waitTicks}: waiting for game screenshot file with prefix: {_pendingPrefix}");
            if (_waitTicks >= TimeoutTicks)
            {
                _mod.Monitor.Warn("Screenshot timed out waiting for game screenshot file.");
                _hud.Hide();
                Cleanup(false);
                _hud.Show(_mod.Helper.Translation.Get("error.timeout"));
            }
            return;
        }

        // File exists but may still be written by the game — wait until it's unlocked
        if (!FileIsReady(latestFile))
        {
            _mod.Monitor.Debug($"File still locked, retrying: {latestFile}");
            if (_waitTicks >= TimeoutTicks)
            {
                _mod.Monitor.Warn("Screenshot timed out waiting for file to be unlocked.");
                _hud.Hide();
                Cleanup(false);
                _hud.Show(_mod.Helper.Translation.Get("error.timeout"));
            }
            return;
        }

        _mod.Monitor.Info($"Game screenshot captured: {latestFile}");

        try
        {
            ProcessScreenshot(latestFile);
            Cleanup(true);
        }
        catch (Exception ex)
        {
            _mod.Monitor.Error($"ProcessScreenshot failed: {ex.Message}");

            Cleanup(false);

            if (ex is UnauthorizedAccessException or IOException)
                _hud.Show(_mod.Helper.Translation.Get("error.disk_full"));
            else
                _hud.Show(string.Format(_mod.Helper.Translation.Get("log.screenshot_failed"), ex.Message));
        }
    }

    /// <summary>
    /// 查找截图开始后创建的新文件
    /// 游戏内置截图命名格式：{prefix}.png（如 FarmHouse.png）
    /// </summary>
    private string? FindLatestScreenshotFile(string prefix)
    {
        if (_screenshotFolder is null || !Directory.Exists(_screenshotFolder))
            return null;

        // 游戏保存的文件名格式是 {prefix}.png
        var expectedFile = Path.Combine(_screenshotFolder, $"{prefix}.png");
        
        if (!File.Exists(expectedFile))
        {
            _mod.Monitor.Debug($"Expected file not found: {expectedFile}");
            return null;
        }

        var fileInfo = new FileInfo(expectedFile);
        
        // 只接受截图开始后创建/修改的文件，避免匹配到旧截图
        if (fileInfo.LastWriteTime < _captureStartTime)
        {
            _mod.Monitor.Debug($"File {fileInfo.Name} was created before capture started ({fileInfo.LastWriteTime:HH:mm:ss} < {_captureStartTime:HH:mm:ss})");
            return null;
        }

        _mod.Monitor.Debug($"Found screenshot file: {fileInfo.Name}");
        return fileInfo.FullName;
    }

    /// <summary>
    /// Loads the game's screenshot PNG, applies grid overlay, saves to mod output folder.
    /// </summary>
    private void ProcessScreenshot(string screenshotPath)
    {
        var gd = Game1.graphics.GraphicsDevice;

        // Load the PNG file as a GPU texture
        // Use FileShare.ReadWrite to allow reading even if another process is writing
        using var stream = new FileStream(screenshotPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sourceTexture = Texture2D.FromStream(gd, stream);

        _mod.Monitor.Debug($"Loaded screenshot: {sourceTexture.Width}x{sourceTexture.Height}");

        // Calculate output dimensions based on configured scale
        float scale = _mod.Config.OutputScale;
        int finalW = Math.Max(1, (int)(sourceTexture.Width * scale));
        int finalH = Math.Max(1, (int)(sourceTexture.Height * scale));

        if (finalW > SafetyLimit || finalH > SafetyLimit)
        {
            float ratio = Math.Min(SafetyLimit / (float)finalW, SafetyLimit / (float)finalH);
            finalW = Math.Max(1, (int)(finalW * ratio));
            finalH = Math.Max(1, (int)(finalH * ratio));
            _mod.Monitor.Warn($"Size exceeds safety limit, scaled to {finalW}x{finalH}");
        }

        _mod.Monitor.Debug($"Final output size: {finalW}x{finalH}");

        var finalRT = _gridRenderer.Apply(sourceTexture, _mod.Config, finalW, finalH);
        try
        {
            string saveDir = ResolveSaveDirectory();
            _mod.Monitor.Debug($"Saving to directory: {saveDir}, prefix: {_pendingPrefix}");
            string savePath = _saver.Save(finalRT, saveDir, _pendingPrefix!, _mod.Config);

            if (_mod.Config.DeleteGameOriginal)
            {
                try
                {
                    // 等待游戏释放文件句柄
                    for (int retry = 0; retry < 10 && !FileIsReady(screenshotPath); retry++)
                        Thread.Sleep(50);

                    File.Delete(screenshotPath);
                    _mod.Monitor.Debug($"Deleted game original: {screenshotPath}");
                }
                catch (Exception ex)
                {
                    _mod.Monitor.Warn($"Failed to delete game original: {ex.Message}");
                    _hud.Hide();
                    _hud.Show(_mod.Helper.Translation.Get("error.delete_failed"));
                }
            }

            _hud.Hide();
            _hud.Show(_mod.Helper.Translation.Get("hud.saved_brief"));

            _mod.Monitor.Info($"Screenshot saved: {savePath}");

            Game1.activeClickableMenu = new ScreenshotResultMenu(savePath, _pendingPrefix!, _mod);
        }
        finally
        {
            finalRT.Dispose();
        }
    }

    /// <summary>
    /// Cleans up any existing screenshot file with the same name to avoid conflicts.
    /// </summary>
    private void TryCleanupExistingScreenshot(string? folder, string? prefix)
    {
        if (folder is null || prefix is null || !Directory.Exists(folder)) return;

        string filePath = Path.Combine(folder, prefix + ".png");
        if (!File.Exists(filePath)) return;

        try
        {
            File.Delete(filePath);
            _mod.Monitor.Debug($"Cleaned up existing screenshot: {filePath}");
        }
        catch (Exception ex)
        {
            _mod.Monitor.Warn($"Failed to clean up existing screenshot: {ex.Message}");
        }
    }



    /// <summary>
    /// Returns true if the file exists and no other process holds a lock on it.
    /// Uses FileShare.None to ensure the game has fully finished writing.
    /// </summary>
    private static bool FileIsReady(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
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

        // Attempt 1: private overload with GameLocation parameter
        //   private String takeMapScreenshot(GameLocation, Single, String, Action)
        _cachedMethodWithLocation ??= FindMethod(game1Type, "takeMapScreenshot",
            typeof(GameLocation), typeof(float), typeof(string), typeof(Action));
        if (_cachedMethodWithLocation is not null)
        {
            _mod.Monitor.Debug("Calling takeMapScreenshot(GameLocation, float, string, Action)");
            return _cachedMethodWithLocation.Invoke(game1, new object?[] { location, scale, name, null! }) as string;
        }

        // Attempt 2: public overload without GameLocation
        //   public String takeMapScreenshot(Nullable<Single>, String, Action)
        _cachedMethodPublic ??= FindMethod(game1Type, "takeMapScreenshot",
            typeof(float?), typeof(string), typeof(Action));
        if (_cachedMethodPublic is not null)
        {
            _mod.Monitor.Debug("Calling takeMapScreenshot(float?, string, Action) with location swap");

            // 保存原始位置，延迟到截图完成后再恢复
            // 因为 takeMapScreenshot 是异步的，渲染过程中需要访问 Game1.currentLocation
            _originalLocation = Game1.currentLocation;
            Game1.currentLocation = location;
            return _cachedMethodPublic.Invoke(game1, new object?[] { (float?)scale, name, null! }) as string;
        }

        _mod.Monitor.Warn("takeMapScreenshot method not found in this game version.");
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
            string grouped = _mod.LocationService.GetGroupedDisplayTitle(loc);
            if (display.Equals(locationName, StringComparison.OrdinalIgnoreCase) ||
                grouped.Equals(locationName, StringComparison.OrdinalIgnoreCase) ||
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
        
        // 恢复 Game1.currentLocation（如果之前被替换）
        if (_originalLocation is not null)
        {
            Game1.currentLocation = _originalLocation;
            _originalLocation = null;
        }
        
        if (!success)
            _hud.Hide();
        _isRendering = false;
        _pendingLocation = null;
        _pendingPrefix = null;
    }
}
