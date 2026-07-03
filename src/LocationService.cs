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
        "BoatTunnel",        // 姜岛船隧道，draw() 有 null 纹理导致崩溃
    };

    private static readonly string[] FilteredLocationPrefixes = { 
        "UndergroundMine",
        "Dungeon",           // 各种地下城
    };
    private static readonly string[] FilteredLocationContains = { 
        "VolcanoDungeon",
        "QuarryMine",        // 采石场矿井
    };

    private IEnumerable<(GameLocation Location, string? ParentName)> EnumerateWithParents()
    {
        foreach (var location in Game1.locations)
        {
            if (IsFiltered(location)) continue;
            yield return (location, null);

            string parentName = location.GetDisplayName() ?? location.Name ?? "Unknown";

            if (location is Farm farm)
            {
                foreach (var building in farm.buildings)
                {
                    if (building.indoors.Value is not null && !IsFiltered(building.indoors.Value))
                        yield return (building.indoors.Value, parentName);
                }
            }
            else if (location is IslandLocation island)
            {
                foreach (var building in island.buildings)
                {
                    if (building.indoors.Value is not null && !IsFiltered(building.indoors.Value))
                        yield return (building.indoors.Value, parentName);
                }
            }
        }
    }

    public IEnumerable<GameLocation> GetLocations()
    {
        foreach (var (location, _) in EnumerateWithParents())
            yield return location;
    }

    public string GetDisplayTitle(GameLocation location)
    {
        return location.GetDisplayName() ?? location.Name ?? "Unknown";
    }

    /// <summary>
    /// Returns "Parent - DisplayName" for building interiors, just "DisplayName" for top-level locations.
    /// Used in GMCM dropdown for visual grouping.
    /// </summary>
    public string GetGroupedDisplayTitle(GameLocation location)
    {
        foreach (var (loc, parent) in EnumerateWithParents())
        {
            if (ReferenceEquals(loc, location))
            {
                string name = loc.GetDisplayName() ?? loc.Name ?? "Unknown";
                return parent is not null ? $"{parent} - {name}" : name;
            }
        }
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
