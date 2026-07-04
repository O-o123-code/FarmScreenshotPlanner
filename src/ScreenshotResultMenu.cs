using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace FarmScreenshotPlanner;

public class ScreenshotResultMenu : IClickableMenu
{
    private const int MenuW = 640;
    private const int MenuH = 420;
    private const int ThumbMaxW = 400;
    private const int ThumbMaxH = 240;

    // 使用最近邻采样避免像素画缩放时网格线粗细不均
    private static readonly SamplerState PointSampler = new()
    {
        Filter = TextureFilter.Point,
        AddressU = TextureAddressMode.Clamp,
        AddressV = TextureAddressMode.Clamp,
    };

    private readonly string _filePath;
    private readonly string _title;
    private readonly ModEntry _mod;
    private readonly List<ClickableComponent> _buttons = new();
    private int _cooldownTimer;
    private Texture2D? _thumbnail;
    private bool _isZoomed;
    private float _zoomLevel;
    private Rectangle _thumbRect;

    public ScreenshotResultMenu(string filePath, string locationDisplayName, ModEntry mod)
        : base(
            (Game1.uiViewport.Width - MenuW) / 2,
            (Game1.uiViewport.Height - MenuH) / 2,
            MenuW, MenuH, false)
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
            mod.Monitor.Warn($"Failed to load thumbnail: {ex.Message}");
        }

        int btnW = 160;
        int btnH = 48;
        int gap = 16;
        int totalW = btnW * 2 + gap;
        int btnY = yPositionOnScreen + height - 64;

        // 如果有缩略图，计算缩略图区域并调整按钮位置
        _thumbRect = Rectangle.Empty;
        if (_thumbnail is not null)
        {
            float scale = Math.Min((float)ThumbMaxW / _thumbnail.Width, (float)ThumbMaxH / _thumbnail.Height);
            int thumbW = (int)(_thumbnail.Width * scale);
            int thumbH = (int)(_thumbnail.Height * scale);
            int thumbX = xPositionOnScreen + (width - thumbW) / 2;
            int thumbY = yPositionOnScreen + 64;
            _thumbRect = new Rectangle(thumbX, thumbY, thumbW, thumbH);
            btnY = _thumbRect.Bottom + 36;
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

    public void HandleScrollWheel(int direction)
    {
        if (!_isZoomed || _thumbnail is null) return;

        const float step = 0.25f;
        const float maxZoom = 4.0f;  // 最大 4 倍缩放
        const float minZoom = 0.25f; // 最小 0.25 倍缩放

        if (direction > 0)
            _zoomLevel = Math.Min(_zoomLevel + step, maxZoom);
        else
            _zoomLevel = Math.Max(_zoomLevel - step, minZoom);
    }

    public override void receiveKeyPress(Keys key)
    {
        if (key == Keys.Escape)
        {
            if (_isZoomed)
            {
                _isZoomed = false;
                return;
            }
            CloseMenu();
            return;
        }
        base.receiveKeyPress(key);
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        // 缩放模式下，点击任意位置退出缩放
        if (_isZoomed)
        {
            _isZoomed = false;
            return;
        }

        // 点击缩略图区域 → 进入缩放预览
        if (_thumbnail is not null && _thumbRect.Contains(x, y))
        {
            _isZoomed = true;
            // 初始倍率：适配屏幕的最大倍率（浮点）
            _zoomLevel = Math.Max(1f, Math.Min(
                (float)Game1.uiViewport.Width / _thumbnail.Width,
                (float)Game1.uiViewport.Height / _thumbnail.Height));
            return;
        }

        foreach (var btn in _buttons)
        {
            if (!btn.containsPoint(x, y)) continue;

            if (btn.name == "open_folder")
            {
                if (_cooldownTimer > 0) return;
                _cooldownTimer = 1500;
                if (!PlatformHelper.TryRevealFileInExplorer(_filePath))
                    _mod.Monitor.Warn($"Failed to reveal file in explorer: {_filePath}");
            }
            else if (btn.name == "close")
            {
                CloseMenu();
            }

            return;
        }

        base.receiveLeftClick(x, y, playSound);
    }

    public override void draw(SpriteBatch b)
    {
        // SMAPI 在调用 draw() 前已调用 b.Begin()，先关闭它的会话
        b.End();

        try
        {
            if (_isZoomed && _thumbnail is not null)
                DrawZoomMode(b);
            else
                DrawNormalMode(b);
        }
        finally
        {
            // 为 SMAPI 的 End() 打开一个会话
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
        }
    }

    private void DrawZoomMode(SpriteBatch b)
    {
        // Session 1: 背景遮罩（默认线性采样）
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
        b.Draw(Game1.fadeToBlackRect,
            new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height),
            Color.Black * 0.85f);
        b.End();

        // Session 2: 缩略图（最近邻采样 + 浮点缩放）
        int drawW = (int)(_thumbnail!.Width * _zoomLevel);
        int drawH = (int)(_thumbnail.Height * _zoomLevel);
        int drawX = (Game1.uiViewport.Width - drawW) / 2;
        int drawY = (Game1.uiViewport.Height - drawH) / 2;

        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, PointSampler,
            DepthStencilState.None, RasterizerState.CullNone);
        b.Draw(_thumbnail, new Rectangle(drawX, drawY, drawW, drawH), Color.White);
        b.End();

        // Session 3: 提示文字 + 倍率标签 + 鼠标（默认线性采样）
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

        // 左上角倍率标签（显示为 "1.25x" 格式）
        string zoomLabel = $"{_zoomLevel:0.00}x";
        Utility.drawTextWithShadow(b, zoomLabel, Game1.dialogueFont,
            new Vector2(20, 20), Color.White);

        // 底部提示
        string hint = _mod.Helper.Translation.Get("ui.zoom_hint");
        Vector2 hintSize = Game1.smallFont.MeasureString(hint);
        int hintY = Math.Min(
            drawY + drawH + 12,
            Game1.uiViewport.Height - (int)hintSize.Y - 12);
        Utility.drawTextWithShadow(b, hint, Game1.smallFont,
            new Vector2((Game1.uiViewport.Width - hintSize.X) / 2, hintY),
            Color.White);

        drawMouse(b);
        b.End();
    }

    private void DrawNormalMode(SpriteBatch b)
    {
        // Session 1: 背景、菜单框、标题、提示文字（默认线性采样）
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

        b.Draw(Game1.fadeToBlackRect,
            new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height),
            Color.Black * 0.3f);

        IClickableMenu.drawTextureBox(
            b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

        Vector2 titleSize = Game1.dialogueFont.MeasureString(_title);
        Utility.drawTextWithShadow(b, _title, Game1.dialogueFont,
            new Vector2(
                xPositionOnScreen + (width - titleSize.X) / 2,
                yPositionOnScreen + 20),
            Game1.textColor);

        if (_thumbnail is not null && _thumbRect != Rectangle.Empty)
        {
            string zoomHint = _mod.Helper.Translation.Get("ui.click_to_zoom");
            Vector2 zoomSize = Game1.smallFont.MeasureString(zoomHint);
            Utility.drawTextWithShadow(b, zoomHint, Game1.smallFont,
                new Vector2(
                    xPositionOnScreen + (width - zoomSize.X) / 2,
                    _thumbRect.Bottom + 4),
                Color.Gray);
        }

        b.End();

        // Session 2: 缩略图（最近邻采样）
        if (_thumbnail is not null && _thumbRect != Rectangle.Empty)
        {
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, PointSampler,
                DepthStencilState.None, RasterizerState.CullNone);
            b.Draw(_thumbnail, _thumbRect, Color.White);
            b.End();
        }

        // Session 3: 按钮 + 鼠标（默认线性采样）
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
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
        b.End();
    }

    private void DisposeThumbnail()
    {
        _thumbnail?.Dispose();
        _thumbnail = null;
    }

    private void CloseMenu()
    {
        DisposeThumbnail();
        Game1.activeClickableMenu = null;
    }
}
