using JetBrains.Annotations;
using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Kingfisher.Patches;

[HarmonyPatch(typeof(PawnDiedOrDownedThoughtsUtility), nameof(PawnDiedOrDownedThoughtsUtility.RemoveLostThoughts))]
public static class Prefix_PawnDiedOrDownedThoughtsUtility_RemoveLostThoughts {
    [UsedImplicitly]
    public static bool Prefix(Pawn pawn) {
        PawnDiedOrDownedThoughtsOptimizer.RemoveLostThoughts(pawn);
        return false;
    }
}
