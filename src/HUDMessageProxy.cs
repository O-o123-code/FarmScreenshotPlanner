using StardewValley;

namespace FarmScreenshotPlanner;

public class HUDMessageProxy
{
    private readonly RollingFileLogger _log;
    private HUDMessage? _message;

    public HUDMessageProxy(RollingFileLogger log)
    {
        _log = log;
    }

    public void Show(string text)
    {
        try
        {
            _message = new HUDMessage(text, HUDMessage.achievement_type) { timeLeft = 5000f };
            Game1.addHUDMessage(_message);
        }
        catch (Exception ex)
        {
            _log.Warn($"HUDMessageProxy.Show failed: {ex.Message}");
        }
    }

    public void Hide()
    {
        try
        {
            if (_message is not null && Game1.hudMessages.Contains(_message))
            {
                Game1.hudMessages.Remove(_message);
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"HUDMessageProxy.Hide failed: {ex.Message}");
        }
        _message = null;
    }
}
