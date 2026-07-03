using StardewValley;
using StardewValley.Locations;

namespace FarmScreenshotPlanner;

public class LocationService
{
    private static readonly HashSet<string> FilteredLocationNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Mine"
    };

    private static readonly string[] FilteredLocationPrefixes = { "UndergroundMine" };
    private static readonly string[] FilteredLocationContains = { "VolcanoDungeon" };

    public IEnumerable<GameLocation> GetLocations()
    {
        foreach (var location in Game1.locations)
        {
            if (IsFiltered(location)) continue;
            yield return location;
            if (location is Farm farm)
            {
                foreach (var building in farm.buildings)
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
