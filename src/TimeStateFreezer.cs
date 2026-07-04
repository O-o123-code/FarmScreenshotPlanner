using Microsoft.Xna.Framework;
using StardewValley;

namespace FarmScreenshotPlanner;

public class TimeStateFreezer
{
    private Color _savedAmbientLight;

    public void Freeze()
    {
        _savedAmbientLight = Game1.ambientLight;
        Game1.ambientLight = Color.White;
    }

    public void Restore()
    {
        Game1.ambientLight = _savedAmbientLight;
    }
}
