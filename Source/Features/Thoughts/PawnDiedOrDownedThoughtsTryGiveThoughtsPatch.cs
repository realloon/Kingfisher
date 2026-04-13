using JetBrains.Annotations;
using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Kingfisher.Features.Thoughts;

[HarmonyPatch(typeof(PawnDiedOrDownedThoughtsUtility), nameof(PawnDiedOrDownedThoughtsUtility.TryGiveThoughts),
    [typeof(Pawn), typeof(DamageInfo?), typeof(PawnDiedOrDownedThoughtsKind)])]
public static class PawnDiedOrDownedThoughtsTryGiveThoughtsPatch {
    [UsedImplicitly]
    public static bool Prefix(Pawn victim, DamageInfo? dinfo, PawnDiedOrDownedThoughtsKind thoughtsKind) {
        if (thoughtsKind != PawnDiedOrDownedThoughtsKind.Died) return true;

        PawnDiedOrDownedThoughtsOptimizer.TryGiveDiedThoughts(victim, dinfo);

        return false;
    }
}
