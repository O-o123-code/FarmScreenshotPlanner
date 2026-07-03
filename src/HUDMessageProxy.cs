using StardewValley;

namespace FarmScreenshotPlanner;

public class HUDMessageProxy
{
    private HUDMessage? _message;

    public void Show(string text)
    {
        _message = new HUDMessage(text, HUDMessage.achievement_type) { timeLeft = 5000f };
        Game1.addHUDMessage(_message);
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
        catch (Exception)
        {
            // 忽略移除失败的情况，避免影响主流程
        }
        _message = null;
    }
}
