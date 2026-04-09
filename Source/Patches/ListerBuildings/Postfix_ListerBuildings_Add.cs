using JetBrains.Annotations;
using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Kingfisher.Patches;

[HarmonyPatch(typeof(ListerBuildings), nameof(ListerBuildings.Add))]
public static class Postfix_ListerBuildings_Add {
    [UsedImplicitly]
    public static void Postfix(ListerBuildings __instance, Building b) {
        if (!ColonistBuildingDefCache.ShouldTrackColonistBuilding(b)) {
            return;
        }

        ColonistBuildingDefCache.NotifyAdded(__instance, b);
    }
}