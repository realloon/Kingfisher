using JetBrains.Annotations;
using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Kingfisher.Features.Buildings;

[HarmonyPatch(typeof(ListerBuildings), nameof(ListerBuildings.Add))]
public static class ListerBuildingsAddPatch {
    [UsedImplicitly]
    public static void Postfix(ListerBuildings __instance, Building b) {
        if (!ListerBuildingsRewrite.ShouldTrack(b)) {
            return;
        }

        ListerBuildingsRewrite.NotifyAdded(__instance, b);
    }
}