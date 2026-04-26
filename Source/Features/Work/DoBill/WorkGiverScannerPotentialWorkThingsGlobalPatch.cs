#if true
using HarmonyLib;
using JetBrains.Annotations;

// ReSharper disable InconsistentNaming

namespace Kingfisher.Features;

[HarmonyPatch(typeof(WorkGiver_Scanner), nameof(WorkGiver_Scanner.PotentialWorkThingsGlobal))]
public static class WorkGiverScannerPotentialWorkThingsGlobalPatch {
    [UsedImplicitly]
    public static bool Prefix(WorkGiver_Scanner __instance, Pawn pawn, ref IEnumerable<Thing>? __result) {
        if (__instance is not WorkGiver_DoBill workGiverDoBill) {
            return true;
        }

        __result = WorkGiverDoBillRewrite.GetPotentialBillGivers(workGiverDoBill, pawn);

        return false;
    }
}
#endif