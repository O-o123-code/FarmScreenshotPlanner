using StardewValley;

namespace FarmScreenshotPlanner;

public class TimeStateFreezer
{
    private bool _wasPaused;
    private int _savedTimeInterval;

    public void Freeze()
    {
        _wasPaused = Game1.paused;
        _savedTimeInterval = Game1.gameTimeInterval;
        Game1.paused = true;
        Game1.gameTimeInterval = 0;
    }

    public void Restore()
    {
        Game1.paused = _wasPaused;
        Game1.gameTimeInterval = _savedTimeInterval;
    }
}
