using HarmonyLib;
using JetBrains.Annotations;

// ReSharper disable InconsistentNaming

namespace Kingfisher.Features;

[HarmonyPatch(typeof(PawnDiedOrDownedThoughtsUtility), nameof(PawnDiedOrDownedThoughtsUtility.TryGiveThoughts),
    [typeof(Pawn), typeof(DamageInfo?), typeof(PawnDiedOrDownedThoughtsKind)])]
public static class TryGiveDiedThoughtsPatch {
    [UsedImplicitly]
    public static bool Prefix(Pawn victim, DamageInfo? dinfo, PawnDiedOrDownedThoughtsKind thoughtsKind) {
        if (thoughtsKind != PawnDiedOrDownedThoughtsKind.Died) {
            return true;
        }

        PawnDiedOrDownedThoughtsRewrite.TryGiveDiedThoughts(victim, dinfo);

        return false;
    }
}