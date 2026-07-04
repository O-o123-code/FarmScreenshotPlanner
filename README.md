# Farm Screenshot Planner

A [Stardew Valley](https://www.stardewvalley.net/) SMAPI mod that captures full-resolution farm screenshots with a customizable tile grid overlay for planning your perfect farm layout.

## Features

- **Full-map screenshots** of any GameLocation — farm, greenhouse, Ginger Island, quarry, etc.
- **Customizable tile grid** — configure color, thickness (1-3px), and opacity
- **Output scale presets** — 25%, 50%, 75%, 100%
- **GMCM integration** — full configuration UI (optional dependency)
- **Location picker** — select a specific location via dropdown (sorted alphabetically)
- **Thumbnail preview** — view screenshot thumbnail in result dialog
- **Skip-wait hotkey** — press X (configurable) to skip the wait and restore game control immediately during rendering
- **Flexible save path** — option to save to game's screenshot folder
- **Instant access** — result dialog with "Open Folder" button
- **Auto-cleanup** — optionally delete the game's original screenshot after processing
- **i18n** — English & Simplified Chinese, other languages welcome

## Requirements

- Stardew Valley 1.6+
- SMAPI 4.0.0+
- (Optional) [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098)

## Installation

1. Install [SMAPI](https://smapi.io/)
2. Download the latest release from [Nexus Mods](https://www.nexusmods.com/stardewvalley/mods/48585)
3. Extract to `Stardew Valley/Mods/FarmScreenshotPlanner`
4. Launch the game

## Usage

Press **J** to capture a full-map screenshot of your current location.  
Press **X** to skip the wait and restore game control immediately during rendering.

Or use the console command: `/farm_screenshot [location_name]`

## Configuration

A `config.json` is generated on first launch. You can edit it manually or use GMCM in-game.

| Setting | Default | Description |
|---|---|---|
| Hotkey | J | Screenshot hotkey |
| CancelHotkey | X | Skip-wait hotkey during rendering |
| SelectedLocation | Current Location | Target location for screenshot |
| OutputScale | 0.25 | Scale preset (25/50/75/100%) |
| SavePath | Screenshots/ | Output directory |
| UseGameScreenshotFolder | false | Save to game's screenshot folder instead |
| DeleteGameOriginal | true | Delete game's original file after grid overlay |
| Grid.Enabled | true | Show tile grid on screenshot |
| Grid.Color | 00000060 | Grid color (RRGGBBAA hex) |
| Grid.Thickness | 1 | Grid line width (1-3) |
| Grid.Opacity | 0.5 | Grid opacity (0-1) |

## Build from Source

```bash
dotnet build
```

Output: `bin/Release/net6.0/FarmScreenshotPlanner.dll`

## Credits

- [Stardew Valley](https://www.stardewvalley.net/) by ConcernedApe
- [SMAPI](https://smapi.io/) by Pathoschild
- [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) by spacechase0

## License

[MIT](LICENSE)
