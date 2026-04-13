using HarmonyLib;

namespace Kingfisher.Patches;

internal static class ProjectileListerOptimizer {
    public static void RemoveProjectile(ListerThings listerThings, Thing thing) {
        FreePatchTargets.RemoveThing(listerThings, thing);
    }
}