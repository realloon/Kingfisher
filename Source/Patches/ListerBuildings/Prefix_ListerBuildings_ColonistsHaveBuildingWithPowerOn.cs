using JetBrains.Annotations;
using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Kingfisher.Patches;

[HarmonyPatch(typeof(ListerBuildings), nameof(ListerBuildings.ColonistsHaveBuildingWithPowerOn))]
public static class Prefix_ListerBuildings_ColonistsHaveBuildingWithPowerOn {
    [UsedImplicitly]
    public static bool Prefix(ListerBuildings __instance, ThingDef def, ref bool __result) {
        var buildings = ColonistBuildingDefCache.GetOrBuild(__instance, def);

        foreach (var t in buildings) {
            var compPowerTrader = t.PowerTraderComp();
            if (compPowerTrader is { PowerOn: false }) {
                continue;
            }

            __result = true;
            return false;
        }

        __result = false;
        return false;
    }
}