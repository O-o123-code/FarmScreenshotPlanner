using xTile;
using xTile.Layers;

namespace FarmScreenshotPlanner.Helpers;

public static class MapLayerExt
{
    public static IEnumerable<string> GetLayerIds(this Map map)
    {
        foreach (var layer in map.Layers)
            yield return layer.Id;
    }

    public static IEnumerable<Layer> GetLayersByNames(this Map map, params string[] names)
    {
        var set = new HashSet<string>(names);
        foreach (var layer in map.Layers)
        {
            if (set.Contains(layer.Id))
                yield return layer;
        }
    }

    public static bool HasLayer(this Map map, string id)
    {
        foreach (var layer in map.Layers)
            if (layer.Id == id) return true;
        return false;
    }
}
