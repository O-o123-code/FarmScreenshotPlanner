using Microsoft.Xna.Framework;
using StardewValley;

namespace FarmScreenshotPlanner;

public class TimeStateFreezer
{
    private bool _wasPaused;
    private int _savedTimeInterval;
    private Color _savedAmbientLight;

    public void Freeze()
    {
        _wasPaused = Game1.paused;
        _savedTimeInterval = Game1.gameTimeInterval;
        _savedAmbientLight = Game1.ambientLight;

        Game1.paused = true;
        Game1.gameTimeInterval = 0;
        // 强制晴天正午光照：纯白环境光，无色彩偏移
        Game1.ambientLight = Color.White;
    }

    public void Restore()
    {
        Game1.paused = _wasPaused;
        Game1.gameTimeInterval = _savedTimeInterval;
        Game1.ambientLight = _savedAmbientLight;
    }
}
