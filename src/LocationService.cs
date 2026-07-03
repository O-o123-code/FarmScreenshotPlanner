using System.Text;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;

namespace FarmScreenshotPlanner;

public class LocationService
{
    private readonly ITranslationHelper? _translations;

    public LocationService(ITranslationHelper? translations = null)
    {
        _translations = translations;
    }

    private static readonly HashSet<string> FilteredLocationNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Mine",
        "BeachNightMarket",      // 夜市可能有纹理加载问题
        "Submarine",             // 潜水艇
        "AbandonedJojaMart",     // 废弃的 Joja 超市
        "BoatTunnel",            // 姜岛船隧道，draw() 有 null 纹理导致崩溃
        "BathHousePool",         // 温泉子区域，与 BathHouse_Entry 重复
        "BathHouseLockerRoom",   // 温泉子区域，与 BathHouse_Entry 重复
    };

    private static readonly string[] FilteredLocationPrefixes = {
        "UndergroundMine",
        "Dungeon",       // 各种地下城
    };
    private static readonly string[] FilteredLocationContains = {
        "VolcanoDungeon",
        "QuarryMine",    // 采石场矿井
    };

    /// <summary>
    /// Location name bases that indicate sub-areas to filter out.
    /// Matches names like "Cellar2", "Cellar3" but not "Cellar" itself.
    /// </summary>
    private static readonly string[] FilteredNamePatterns = {
        "Cellar",
    };

    private IEnumerable<(GameLocation Location, string? ParentName)> EnumerateWithParents()
    {
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var location in Game1.locations)
        {
            if (IsFiltered(location)) continue;

            if (location.Name is not null && !seenNames.Add(location.Name))
                continue;

            yield return (location, null);

            string parentName = GetDisplayTitle(location);

            if (location is Farm farm)
            {
                foreach (var building in farm.buildings)
                {
                    var indoors = building.indoors.Value;
                    if (indoors is null || IsFiltered(indoors)) continue;
                    if (indoors.Name is not null && !seenNames.Add(indoors.Name)) continue;
                    yield return (indoors, parentName);
                }
            }
            else if (location is IslandLocation island)
            {
                foreach (var building in island.buildings)
                {
                    var indoors = building.indoors.Value;
                    if (indoors is null || IsFiltered(indoors)) continue;
                    if (indoors.Name is not null && !seenNames.Add(indoors.Name)) continue;
                    yield return (indoors, parentName);
                }
            }
        }
    }

    public IEnumerable<GameLocation> GetLocations()
    {
        foreach (var (location, _) in EnumerateWithParents())
            yield return location;
    }

    /// <summary>
    /// Resolves a human-readable display title for a location.
    /// Priority: i18n translation → game GetDisplayName → camelCase-split internal name.
    /// </summary>
    public string GetDisplayTitle(GameLocation location)
    {
        // 1. Try i18n translation (e.g. "location.IslandFarmCave" → "姜岛农场洞穴")
        if (_translations is not null && location.Name is not null)
        {
            string translated = _translations.Get($"location.{location.Name}");
            // SMAPI returns "(no translation:key)" when key is missing
            if (!translated.StartsWith("(no translation:"))
                return translated;
        }

        // 2. Try game's built-in display name
        string? displayName = location.GetDisplayName();
        if (!string.IsNullOrEmpty(displayName) && displayName != location.Name)
            return displayName;

        // 3. Fallback: split camelCase internal name for readability
        return location.Name is not null ? SplitCamelCase(location.Name) : "Unknown";
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
                string name = GetDisplayTitle(loc);
                return parent is not null ? $"{parent} - {name}" : name;
            }
        }
        return GetDisplayTitle(location);
    }

    /// <summary>
    /// Returns true if the given location is dangerous or unsuitable for screenshots.
    /// </summary>
    public static bool IsDangerousLocation(GameLocation loc) => IsFiltered(loc);

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

        if (IsSubArea(name)) return true;

        return false;
    }

    private static bool IsSubArea(string name)
    {
        foreach (var pattern in FilteredNamePatterns)
        {
            if (name.Length > pattern.Length
                && name.StartsWith(pattern, StringComparison.OrdinalIgnoreCase)
                && name[pattern.Length..].All(char.IsDigit))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Splits camelCase/PascalCase names into space-separated words.
    /// e.g. "IslandFarmCave" → "Island Farm Cave", "FarmHouse" → "Farm House"
    /// </summary>
    private static string SplitCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        var sb = new StringBuilder();
        sb.Append(name[0]);

        for (int i = 1; i < name.Length; i++)
        {
            char c = name[i];
            char prev = name[i - 1];

            // Insert space before: uppercase after lowercase, or uppercase followed by lowercase after uppercase
            if (char.IsUpper(c) && char.IsLower(prev))
                sb.Append(' ');
            else if (i + 1 < name.Length && char.IsUpper(c) && char.IsUpper(prev) && char.IsLower(name[i + 1]))
                sb.Append(' ');

            // Replace underscores with spaces
            if (c == '_')
                sb.Append(' ');
            else
                sb.Append(c);
        }

        return sb.ToString();
    }
}
