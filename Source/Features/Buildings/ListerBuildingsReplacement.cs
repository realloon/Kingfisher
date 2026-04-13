using Kingfisher.Prepatching;

namespace Kingfisher.Features.Buildings;

internal static class ListerBuildingsReplacement {
    public static List<Building> AllBuildingsColonistOfDef(ListerBuildings listerBuildings, ThingDef def) =>
        ColonistBuildingDefCache.CopyBuildingsOfDef(listerBuildings, def);

    public static bool ColonistsHaveBuilding(ListerBuildings listerBuildings, ThingDef def) =>
        ColonistBuildingDefCache.GetOrBuild(listerBuildings, def).Count > 0;

    public static bool ColonistsHaveBuildingWithPowerOn(ListerBuildings listerBuildings, ThingDef def) {
        var buildings = ColonistBuildingDefCache.GetOrBuild(listerBuildings, def);
        foreach (var building in buildings) {
            var compPowerTrader = building.PowerTraderComp();
            if (compPowerTrader is { PowerOn: false }) {
                continue;
            }

            return true;
        }

        return false;
    }
}
