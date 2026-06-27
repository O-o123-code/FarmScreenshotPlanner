using StardewValley;

namespace FarmScreenshotPlanner;

public class ScreenshotOrchestrator
{
    private readonly ModEntry _mod;
    private readonly MapRenderer _mapRenderer = new();
    private readonly TileGridRenderer _tileGridRenderer = new();
    private readonly ScreenshotSaver _saver = new();
    private readonly TimeStateFreezer _freezer = new();
    private readonly HUDMessageProxy _hud = new();

    private bool _isRendering;

    private const int SafetyLimit = 16384;

    public bool IsRendering => _isRendering;

    public ScreenshotOrchestrator(ModEntry mod)
    {
        _mod = mod;
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
            var location = ResolveLocation(locationName);
            if (location is null)
            {
                _mod.LogFile.Warn("No valid location found for screenshot.");
                return;
            }

            _freezer.Freeze();
            _hud.Show(_mod.Helper.Translation.Get("hud.rendering"));

            var map = location.Map;
            int mapPixelW = map.Layers[0].LayerWidth * 64;
            int mapPixelH = map.Layers[0].LayerHeight * 64;

            var fullRT = _mapRenderer.Render(location).FullRT;

            float scale = _mod.Config.OutputScale;
            int finalW = (int)(mapPixelW * scale);
            int finalH = (int)(mapPixelH * scale);

            if (finalW > SafetyLimit || finalH > SafetyLimit)
            {
                float ratio = Math.Min(SafetyLimit / (float)finalW, SafetyLimit / (float)finalH);
                finalW = (int)(finalW * ratio);
                finalH = (int)(finalH * ratio);
                _mod.LogFile.Warn($"Map size {mapPixelW}x{mapPixelH} exceeds safety limit, scaled to {finalW}x{finalH}");
            }

            var finalRT = _tileGridRenderer.Apply(fullRT, _mod.Config, finalW, finalH, mapPixelW, mapPixelH);

            string saveDir = ResolveSaveDirectory();
            string prefix = _mod.LocationService.GetDisplayTitle(location);
            string savePath = _saver.Save(finalRT, saveDir, prefix);

            _freezer.Restore();
            _hud.Hide();

            _mod.LogFile.Info($"Screenshot saved: {savePath}");
            _mod.Monitor.Log($"Screenshot saved: {savePath}", LogLevel.Info);

            Game1.activeClickableMenu = new ScreenshotResultMenu(savePath, _mod);
        }
        catch (Exception ex)
        {
            _freezer.Restore();
            _hud.Hide();

            _mod.LogFile.Error($"Screenshot failed: {ex.Message}");
            _mod.Monitor.Log($"Screenshot failed: {ex}", LogLevel.Error);

            if (ex is UnauthorizedAccessException or IOException)
            {
                _hud.Show(_mod.Helper.Translation.Get("error.disk_full"));
            }
            else
            {
                _hud.Show(string.Format(_mod.Helper.Translation.Get("log.screenshot_failed"), ex.Message));
            }
        }
        finally
        {
            _isRendering = false;
        }
    }

    private GameLocation? ResolveLocation(string? locationName)
    {
        if (string.IsNullOrEmpty(locationName) || locationName == "Current Location")
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
        return string.IsNullOrEmpty(_mod.Config.SavePath)
            ? Path.Combine(_mod.Helper.DirectoryPath, "Screenshots")
            : _mod.Config.SavePath;
    }
}
