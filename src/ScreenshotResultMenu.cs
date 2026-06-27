using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace FarmScreenshotPlanner;

public class ScreenshotResultMenu : IClickableMenu
{
    private readonly string _filePath;
    private readonly ModEntry _mod;
    private readonly List<ClickableComponent> _buttons = new();
    private int _cooldownTimer;

    public ScreenshotResultMenu(string filePath, ModEntry mod)
        : base(
            (Game1.uiViewport.Width - 480) / 2,
            (Game1.uiViewport.Height - 220) / 2,
            480, 220)
    {
        _filePath = filePath;
        _mod = mod;

        exitFunction = null;
        _cooldownTimer = 0;

        string openLabel = mod.Helper.Translation.Get("ui.open_folder");
        string closeLabel = mod.Helper.Translation.Get("ui.close");

        int btnW = 140;
        int btnH = 48;
        int gap = 16;
        int totalW = btnW * 2 + gap;
        int btnY = yPositionOnScreen + height - 64;

        _buttons.Add(new ClickableComponent(
            new Rectangle(
                xPositionOnScreen + (width - totalW) / 2,
                btnY, btnW, btnH),
            "open_folder")
        {
            myID = 0,
            rightNeighborID = 1,
        });

        _buttons.Add(new ClickableComponent(
            new Rectangle(
                xPositionOnScreen + (width - totalW) / 2 + btnW + gap,
                btnY, btnW, btnH),
            "close")
        {
            myID = 1,
            leftNeighborID = 0,
        });
    }

    public override void update(GameTime time)
    {
        base.update(time);
        if (_cooldownTimer > 0)
            _cooldownTimer -= time.ElapsedGameTime.Milliseconds;
    }

    public override void receiveKeyPress(Keys key)
    {
        if (key == Keys.Escape) return;
        base.receiveKeyPress(key);
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        foreach (var btn in _buttons)
        {
            if (!btn.containsPoint(x, y)) continue;

            if (btn.name == "open_folder")
            {
                if (_cooldownTimer > 0) return;
                _cooldownTimer = 1500;
                PlatformHelper.RevealFileInExplorer(_filePath);
            }
            else if (btn.name == "close")
                Game1.activeClickableMenu = null;

            return;
        }

        base.receiveLeftClick(x, y, playSound);
    }

    public override void draw(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect,
            new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height),
            Color.Black * 0.3f);

        IClickableMenu.drawTextureBox(
            b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

        string title = Path.GetFileName(_filePath);
        Vector2 titleSize = Game1.dialogueFont.MeasureString(title);
        Utility.drawTextWithShadow(b, title, Game1.dialogueFont,
            new Vector2(
                xPositionOnScreen + (width - titleSize.X) / 2,
                yPositionOnScreen + 28),
            Game1.textColor);

        string pathText = _filePath;
        float maxPathW = width - 32;
        var font = Game1.smallFont;
        if (font.MeasureString(pathText).X > maxPathW)
        {
            int maxLen = (int)(pathText.Length * maxPathW / font.MeasureString(pathText).X) - 3;
            if (maxLen < 1) maxLen = 1;
            while (font.MeasureString("..." + pathText[^maxLen..]).X > maxPathW)
                maxLen--;
            pathText = "..." + pathText[^maxLen..];
        }
        Utility.drawTextWithShadow(b, pathText, Game1.smallFont,
            new Vector2(xPositionOnScreen + 16, yPositionOnScreen + 80),
            Game1.textColor);

        foreach (var btn in _buttons)
        {
            string label = btn.name == "open_folder"
                ? _mod.Helper.Translation.Get("ui.open_folder")
                : _mod.Helper.Translation.Get("ui.close");

            IClickableMenu.drawTextureBox(
                b, btn.bounds.X, btn.bounds.Y, btn.bounds.Width, btn.bounds.Height, Color.White);

            Vector2 labelSize = Game1.dialogueFont.MeasureString(label);
            Utility.drawTextWithShadow(b, label, Game1.dialogueFont,
                new Vector2(
                    btn.bounds.X + (btn.bounds.Width - labelSize.X) / 2,
                    btn.bounds.Y + (btn.bounds.Height - labelSize.Y) / 2),
                Game1.textColor);
        }

        drawMouse(b);
    }
}
