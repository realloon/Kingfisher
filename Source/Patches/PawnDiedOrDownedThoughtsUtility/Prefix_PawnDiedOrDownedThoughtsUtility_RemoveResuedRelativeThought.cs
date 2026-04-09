using JetBrains.Annotations;
using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Kingfisher.Patches;

[HarmonyPatch(typeof(PawnDiedOrDownedThoughtsUtility),
    nameof(PawnDiedOrDownedThoughtsUtility.RemoveResuedRelativeThought))]
public static class Prefix_PawnDiedOrDownedThoughtsUtility_RemoveResuedRelativeThought {
    [UsedImplicitly]
    public static bool Prefix(Pawn pawn) {
        PawnDiedOrDownedThoughtsOptimizer.RemoveResuedRelativeThought(pawn);
        return false;
    }
}
