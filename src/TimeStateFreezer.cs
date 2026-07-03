using Microsoft.Xna.Framework;
using StardewValley;

namespace FarmScreenshotPlanner;

public class TimeStateFreezer
{
    private Color _savedAmbientLight;
    private bool _wasPaused;

    public void Freeze()
    {
        _savedAmbientLight = Game1.ambientLight;
        Game1.ambientLight = Color.White;
        _wasPaused = Game1.paused;
        Game1.paused = true;
    }

    public void Restore()
    {
        Game1.ambientLight = _savedAmbientLight;
        Game1.paused = _wasPaused;
    }
}
