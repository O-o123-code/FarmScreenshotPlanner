using StardewValley;
using StardewValley.Locations;

namespace FarmScreenshotPlanner;

public class LocationService
{
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
        return loc is MineShaft
            || loc.Name?.StartsWith("UndergroundMine") == true
            || loc.Name == "Mine"
            || loc.Name?.Contains("VolcanoDungeon") == true;
    }
}
