using StardewValley;
using StardewValley.Locations;

namespace FarmScreenshotPlanner;

public class LocationService
{
    private static readonly HashSet<string> FilteredLocationNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Mine",
        "BeachNightMarket",  // 夜市可能有纹理加载问题
        "Submarine",         // 潜水艇
        "AbandonedJojaMart", // 废弃的 Joja 超市
    };

    private static readonly string[] FilteredLocationPrefixes = { 
        "UndergroundMine",
        "Dungeon",           // 各种地下城
    };
    private static readonly string[] FilteredLocationContains = { 
        "VolcanoDungeon",
        "QuarryMine",        // 采石场矿井
    };

    public IEnumerable<GameLocation> GetLocations()
    {
        foreach (var location in Game1.locations)
        {
            if (IsFiltered(location)) continue;
            yield return location;
            
            // 展开 Farm 类型的建筑内部
            if (location is Farm farm)
            {
                foreach (var building in farm.buildings)
                {
                    if (building.indoors.Value is not null && !IsFiltered(building.indoors.Value))
                        yield return building.indoors.Value;
                }
            }
            // 展开 IslandLocation（姜岛）的建筑内部
            else if (location is IslandLocation island)
            {
                foreach (var building in island.buildings)
                {
                    if (building.indoors.Value is not null && !IsFiltered(building.indoors.Value))
                        yield return building.indoors.Value;
                }
            }
        }
    }

    public string GetDisplayTitle(GameLocation location)
    {
        return location.GetDisplayName() ?? location.Name ?? "Unknown";
    }

    private static bool IsFiltered(GameLocation loc)
    {
        if (loc is MineShaft) return true;

        string? name = loc.Name;
        if (string.IsNullOrEmpty(name)) return false;

        if (FilteredLocationNames.Contains(name)) return true;

        foreach (var prefix in FilteredLocationPrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
        }

        foreach (var contains in FilteredLocationContains)
        {
            if (name.Contains(contains, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }
}
