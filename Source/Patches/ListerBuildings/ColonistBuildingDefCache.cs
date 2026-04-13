using Prepatcher;

namespace Kingfisher.Patches;

internal static class ColonistBuildingDefCache {
    private static readonly List<Building> ResultBuffer = [];

    [PrepatcherField]
    [ValueInitializer(nameof(CreateCache))]
    private static extern ref Dictionary<ThingDef, List<Building>> ColonistBuildingsByDef(this ListerBuildings target);

    public static bool ShouldTrackColonistBuilding(Building building) =>
        building.Faction == Faction.OfPlayer && building.def.building is not { isNaturalRock: true };

    public static List<Building> CopyBuildingsOfDef(ListerBuildings listerBuildings, ThingDef def) {
        ResultBuffer.Clear();
        ResultBuffer.AddRange(GetOrBuild(listerBuildings, def));
        return ResultBuffer;
    }

    public static List<Building> GetOrBuild(ListerBuildings listerBuildings, ThingDef def) {
        var cache = listerBuildings.ColonistBuildingsByDef();
        if (cache.TryGetValue(def, out var buildings)) {
            return buildings;
        }

        buildings = [];
        var allBuildingsColonist = listerBuildings.allBuildingsColonist;
        foreach (var building in allBuildingsColonist) {
            if (building.def == def) {
                buildings.Add(building);
            }
        }

        cache.Add(def, buildings);
        return buildings;
    }

    public static void NotifyAdded(ListerBuildings listerBuildings, Building building) {
        var cache = listerBuildings.ColonistBuildingsByDef();
        if (cache.TryGetValue(building.def, out var buildings)) {
            buildings.Add(building);
        }
    }

    public static void NotifyRemoved(ListerBuildings listerBuildings, Building building) {
        var cache = listerBuildings.ColonistBuildingsByDef();
        if (!cache.TryGetValue(building.def, out var buildings)) {
            return;
        }

        var index = buildings.LastIndexOf(building);
        if (index >= 0) {
            buildings.RemoveAt(index);
        }
    }

    private static Dictionary<ThingDef, List<Building>> CreateCache() => [];
}