using System;
using System.Diagnostics;
using System.IO;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace FarmBlueprint;

/// <summary>
/// SMAPI 模组入口
/// </summary>
public class ModEntry : Mod
{
    private ModConfig _config = null!;
    private BlueprintScreenshot _screenshot = null!;
    private bool _isCapturing;

    public override void Entry(IModHelper helper)
    {
        // 加载配置
        _config = helper.ReadConfig<ModConfig>();

        // 初始化截图服务
        _screenshot = new BlueprintScreenshot(Monitor, helper, _config);

        // 注册事件
        helper.Events.Input.ButtonsChanged += OnButtonsChanged;
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
    }

    /// <summary>
    /// 游戏启动后注册 GMCM 配置菜单
    /// </summary>
    private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
    {
        // 获取 GMCM API
        var gmcm = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (gmcm is null)
        {
            Monitor.Log("GenericModConfigMenu not found, config will use config.json only.", LogLevel.Info);
            return;
        }

        gmcm.Register(
            mod: ModManifest,
            reset: () => _config = new ModConfig(),
            save: () => Helper.WriteConfig(_config)
        );

        // === 快捷键 ===
        gmcm.AddKeybind(
            mod: ModManifest,
            name: () => Helper.Translation.Get("Config.ScreenshotKey.Name"),
            tooltip: () => Helper.Translation.Get("Config.ScreenshotKey.Desc"),
            getValue: () => _config.ScreenshotKey,
            setValue: value => _config.ScreenshotKey = value
        );

        // === 截图区域 ===
        gmcm.AddTextOption(
            mod: ModManifest,
            name: () => Helper.Translation.Get("Config.CaptureArea.Name"),
            tooltip: () => Helper.Translation.Get("Config.CaptureArea.Desc"),
            getValue: () => _config.CaptureArea.ToString(),
            setValue: value => _config.CaptureArea = Enum.Parse<CaptureArea>(value),
            allowedValues: Enum.GetNames(typeof(CaptureArea))
        );

        // === 启用网格 ===
        gmcm.AddBoolOption(
            mod: ModManifest,
            name: () => Helper.Translation.Get("Config.EnableGrid.Name"),
            tooltip: () => Helper.Translation.Get("Config.EnableGrid.Desc"),
            getValue: () => _config.EnableGrid,
            setValue: value => _config.EnableGrid = value
        );

        // === 网格颜色 ===
        gmcm.AddTextOption(
            mod: ModManifest,
            name: () => Helper.Translation.Get("Config.GridColor.Name"),
            tooltip: () => Helper.Translation.Get("Config.GridColor.Desc"),
            getValue: () => _config.GridColor,
            setValue: value => _config.GridColor = value
        );

        // === 网格粗细 ===
        gmcm.AddNumberOption(
            mod: ModManifest,
            name: () => Helper.Translation.Get("Config.GridThickness.Name"),
            tooltip: () => Helper.Translation.Get("Config.GridThickness.Desc"),
            getValue: () => _config.GridThickness,
            setValue: value => _config.GridThickness = value,
            min: 1,
            max: 3
        );

        // === 网格透明度 ===
        gmcm.AddNumberOption(
            mod: ModManifest,
            name: () => Helper.Translation.Get("Config.GridOpacity.Name"),
            tooltip: () => Helper.Translation.Get("Config.GridOpacity.Desc"),
            getValue: () => _config.GridOpacity,
            setValue: value => _config.GridOpacity = value,
            min: 0,
            max: 255
        );

        // === 仅截图时显示网格 ===
        gmcm.AddBoolOption(
            mod: ModManifest,
            name: () => Helper.Translation.Get("Config.GridOnlyOnScreenshot.Name"),
            tooltip: () => Helper.Translation.Get("Config.GridOnlyOnScreenshot.Desc"),
            getValue: () => _config.GridOnlyOnScreenshot,
            setValue: value => _config.GridOnlyOnScreenshot = value
        );

        // === 自动打开文件夹 ===
        gmcm.AddBoolOption(
            mod: ModManifest,
            name: () => Helper.Translation.Get("Config.AutoOpenFolder.Name"),
            tooltip: () => Helper.Translation.Get("Config.AutoOpenFolder.Desc"),
            getValue: () => _config.AutoOpenFolder,
            setValue: value => _config.AutoOpenFolder = value
        );

        // === 文件名前缀 ===
        gmcm.AddTextOption(
            mod: ModManifest,
            name: () => Helper.Translation.Get("Config.FileNamePrefix.Name"),
            tooltip: () => Helper.Translation.Get("Config.FileNamePrefix.Desc"),
            getValue: () => _config.FileNamePrefix,
            setValue: value => _config.FileNamePrefix = value
        );
    }

    /// <summary>
    /// 监听按键，触发截图
    /// </summary>
    private void OnButtonsChanged(object sender, ButtonsChangedEventArgs e)
    {
        if (_isCapturing)
            return;

        if (Context.IsPlayerFree && Helper.Input.IsDown(_config.ScreenshotKey))
        {
            _isCapturing = true;
            CaptureScreenshot();
        }
    }

    /// <summary>
    /// 执行截图（渲染在主线程，保存在后台）
    /// </summary>
    private void CaptureScreenshot()
    {
        // 显示提示
        Game1.addHUDMessage(new HUDMessage(Helper.Translation.Get("Message.Capturing"), 2));

        // GPU 渲染在主线程，文件保存在后台
        _screenshot.CaptureAsync(_config.CaptureArea, OnCaptureComplete);
    }

    /// <summary>
    /// 截图完成回调
    /// </summary>
    private void OnCaptureComplete(string filePath, string errorMessage)
    {
        _isCapturing = false;

        Helper.Events.GameLoop.UpdateTicked += ShowResultMessage;

        void ShowResultMessage(object sender, UpdateTickedEventArgs args)
        {
            // 延迟一帧确保在游戏主线程显示
            Helper.Events.GameLoop.UpdateTicked -= ShowResultMessage;

            if (!string.IsNullOrEmpty(errorMessage))
            {
                Game1.addHUDMessage(new HUDMessage(errorMessage, 1));
                return;
            }

            if (!string.IsNullOrEmpty(filePath))
            {
                var msg = Helper.Translation.Get("Message.Saved", new { path = filePath });
                Game1.addHUDMessage(new HUDMessage(msg, 2));

                if (_config.AutoOpenFolder)
                {
                    var dir = Path.GetDirectoryName(filePath);
                    if (Directory.Exists(dir))
                        Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
                }
            }
        }
    }

    /// <summary>
    /// GMCM API 接口定义
    /// </summary>
    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save);
        void AddKeybind(IManifest mod, Func<string> name, Func<string> tooltip, Func<SButton> getValue, Action<SButton> setValue);
        void AddTextOption(IManifest mod, Func<string> name, Func<string> tooltip, Func<string> getValue, Action<string> setValue, string[] allowedValues = null);
        void AddBoolOption(IManifest mod, Func<string> name, Func<string> tooltip, Func<bool> getValue, Action<bool> setValue);
        void AddNumberOption(IManifest mod, Func<string> name, Func<string> tooltip, Func<int> getValue, Action<int> setValue, int min, int max, int interval = 1);
    }
}
