namespace Kingfisher.Features.Thoughts;

internal static class PawnDiedOrDownedThoughtsReplacement {
    public static void RemoveLostThoughts(Pawn pawn) => PawnDiedOrDownedThoughtsOptimizer.RemoveLostThoughts(pawn);

    public static void RemoveResuedRelativeThought(Pawn pawn) =>
        PawnDiedOrDownedThoughtsOptimizer.RemoveResuedRelativeThought(pawn);
}
