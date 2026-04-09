using JetBrains.Annotations;
using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Kingfisher.Patches;

[HarmonyPatch(typeof(ListerBuildings), nameof(ListerBuildings.AllBuildingsColonistOfDef))]
public static class Prefix_ListerBuildings_AllBuildingsColonistOfDef {
    [UsedImplicitly]
    public static bool Prefix(ListerBuildings __instance, ThingDef def, ref List<Building> __result) {
        __result = ColonistBuildingDefCache.CopyBuildingsOfDef(__instance, def);
        return false;
    }
}