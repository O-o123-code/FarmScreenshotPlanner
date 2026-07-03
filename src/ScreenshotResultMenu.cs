using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace FarmScreenshotPlanner;

public class ScreenshotResultMenu : IClickableMenu
{
    private readonly string _filePath;
    private readonly string _title;
    private readonly ModEntry _mod;
    private readonly List<ClickableComponent> _buttons = new();
    private int _cooldownTimer;
    private Texture2D? _thumbnail;

    public ScreenshotResultMenu(string filePath, string locationDisplayName, ModEntry mod)
        : base(
            (Game1.uiViewport.Width - 480) / 2,
            (Game1.uiViewport.Height - 220) / 2,
            480, 220, false)
    {
        _filePath = filePath;
        _mod = mod;
        _title = string.Format(mod.Helper.Translation.Get("ui.saved_title"), locationDisplayName);

        exitFunction = null;
        _cooldownTimer = 0;

        // 加载缩略图
        try
        {
            if (File.Exists(filePath))
            {
                using var stream = File.OpenRead(filePath);
                _thumbnail = Texture2D.FromStream(Game1.graphics.GraphicsDevice, stream);
            }
        }
        catch (Exception ex)
        {
            mod.LogFile.Warn($"Failed to load thumbnail: {ex.Message}");
        }

        string openLabel = mod.Helper.Translation.Get("ui.open_folder");
        string closeLabel = mod.Helper.Translation.Get("ui.close");

        int btnW = 140;
        int btnH = 48;
        int gap = 16;
        int totalW = btnW * 2 + gap;
        int btnY = yPositionOnScreen + height - 64;

        // 如果有缩略图，调整按钮位置
        if (_thumbnail is not null)
        {
            btnY = yPositionOnScreen + 170;
        }

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
        if (key == Keys.Escape)
        {
            Game1.activeClickableMenu = null;
            return;
        }
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
                if (!PlatformHelper.TryRevealFileInExplorer(_filePath))
                    _mod.LogFile.Warn($"Failed to reveal file in explorer: {_filePath}");
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

        Vector2 titleSize = Game1.dialogueFont.MeasureString(_title);
        Utility.drawTextWithShadow(b, _title, Game1.dialogueFont,
            new Vector2(
                xPositionOnScreen + (width - titleSize.X) / 2,
                yPositionOnScreen + 28),
            Game1.textColor);

        // 绘制缩略图
        if (_thumbnail is not null)
        {
            int thumbMaxW = 160;
            int thumbMaxH = 100;
            float scale = Math.Min((float)thumbMaxW / _thumbnail.Width, (float)thumbMaxH / _thumbnail.Height);
            int thumbW = (int)(_thumbnail.Width * scale);
            int thumbH = (int)(_thumbnail.Height * scale);
            int thumbX = xPositionOnScreen + (width - thumbW) / 2;
            int thumbY = yPositionOnScreen + 60;

            b.Draw(_thumbnail, new Rectangle(thumbX, thumbY, thumbW, thumbH), Color.White);
        }

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
