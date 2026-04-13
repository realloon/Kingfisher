using JetBrains.Annotations;
using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Kingfisher.Patches;

[HarmonyPatch(typeof(ListerBuildings), nameof(ListerBuildings.Remove))]
public static class Prefix_ListerBuildings_Remove {
    [UsedImplicitly]
    public static void Prefix(ListerBuildings __instance, Building b) {
        if (!ColonistBuildingDefCache.ShouldTrackColonistBuilding(b)) {
            return;
        }

        ColonistBuildingDefCache.NotifyRemoved(__instance, b);
    }
}