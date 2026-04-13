using Verse.AI;

namespace Kingfisher.Features.Combat;

internal static class AttackTargetFinderReplacement {
    public static IAttackTarget? BestAttackTarget(IAttackTargetSearcher searcher, TargetScanFlags flags,
        Predicate<Thing>? validator, float minDist, float maxDist, IntVec3 locus, float maxTravelRadiusFromLocus,
        bool canBashDoors, bool canTakeTargetsCloserThanEffectiveMinRange, bool canBashFences, bool onlyRanged) =>
        AttackTargetFinderOptimizer.BestAttackTarget(
            searcher,
            flags,
            validator,
            minDist,
            maxDist,
            locus,
            maxTravelRadiusFromLocus,
            canBashDoors,
            canTakeTargetsCloserThanEffectiveMinRange,
            canBashFences,
            onlyRanged
        );
}
