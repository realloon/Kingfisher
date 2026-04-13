using JetBrains.Annotations;
using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Kingfisher.Features.Buildings;

[HarmonyPatch(typeof(ListerBuildings), nameof(ListerBuildings.Add))]
public static class ListerBuildingsAddPatch {
    [UsedImplicitly]
    public static void Postfix(ListerBuildings __instance, Building b) {
        if (!ColonistBuildingDefCache.ShouldTrackColonistBuilding(b)) {
            return;
        }

        ColonistBuildingDefCache.NotifyAdded(__instance, b);
    }
}
