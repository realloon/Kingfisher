using JetBrains.Annotations;
using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Kingfisher.Patches;

[HarmonyPatch(typeof(ListerBuildings), nameof(ListerBuildings.ColonistsHaveBuilding), [typeof(ThingDef)])]
public static class Prefix_ListerBuildings_ColonistsHaveBuilding {
    [UsedImplicitly]
    public static bool Prefix(ListerBuildings __instance, ThingDef def, ref bool __result) {
        __result = ColonistBuildingDefCache.GetOrBuild(__instance, def).Count > 0;
        return false;
    }
}