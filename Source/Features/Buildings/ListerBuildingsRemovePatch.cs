using JetBrains.Annotations;
using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Kingfisher.Features.Buildings;

[HarmonyPatch(typeof(ListerBuildings), nameof(ListerBuildings.Remove))]
public static class ListerBuildingsRemovePatch {
    [UsedImplicitly]
    public static void Prefix(ListerBuildings __instance, Building b) {
        if (!ListerBuildingsRewrite.ShouldTrack(b)) {
            return;
        }

        ListerBuildingsRewrite.NotifyRemoved(__instance, b);
    }
}