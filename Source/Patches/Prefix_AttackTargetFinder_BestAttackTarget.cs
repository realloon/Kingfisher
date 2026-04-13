using JetBrains.Annotations;
using HarmonyLib;
using UnityEngine;
using Verse.AI;
using Verse.AI.Group;

// ReSharper disable InconsistentNaming

namespace Kingfisher.Patches;

[HarmonyPatch(typeof(AttackTargetFinder), nameof(AttackTargetFinder.BestAttackTarget))]
public static class Prefix_AttackTargetFinder_BestAttackTarget {
    [UsedImplicitly]
    public static bool Prefix(ref IAttackTarget? __result, IAttackTargetSearcher searcher, TargetScanFlags flags,
        Predicate<Thing>? validator, float minDist, float maxDist, IntVec3 locus, float maxTravelRadiusFromLocus,
        bool canBashDoors, bool canTakeTargetsCloserThanEffectiveMinRange, bool canBashFences, bool onlyRanged) {
        __result = AttackTargetFinderOptimizer.BestAttackTarget(
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

        return false;
    }
}

internal static class AttackTargetFinderOptimizer {
    private static readonly List<IAttackTarget> ShootableTargets = new(32);
    private static readonly List<IAttackTarget> FallbackTargets = new(128);
    private static readonly List<float> ShootableTargetScores = new(32);
    private static readonly List<Pair<IAttackTarget, float>> WeightedTargets = new(32);

    private static GetShootingTargetScoreDelegate? _getShootingTargetScore;
    private static FindBestReachableMeleeTargetDelegate? _findBestReachableMeleeTarget;

    private delegate float GetShootingTargetScoreDelegate(IAttackTarget target, IAttackTargetSearcher searcher,
        Verb verb);

    private delegate IAttackTarget? FindBestReachableMeleeTargetDelegate(Predicate<IAttackTarget> validator,
        Pawn searcherPawn, float maxTargDist, bool canBashDoors, bool canBashFences);

    public static IAttackTarget? BestAttackTarget(IAttackTargetSearcher searcher, TargetScanFlags flags,
        Predicate<Thing>? validator, float minDist, float maxDist, IntVec3 locus, float maxTravelRadiusFromLocus,
        bool canBashDoors, bool canTakeTargetsCloserThanEffectiveMinRange, bool canBashFences, bool onlyRanged) {
        EnsureDelegatesInitialized();

        var searcherThing = searcher.Thing;
        var searcherPawn = searcher as Pawn;
        var verb = searcher.CurrentEffectiveVerb;

        if (verb == null) {
            Log.Error("BestAttackTarget with " + searcher.ToStringSafe() + " who has no attack verb.");
            return null;
        }

        var onlyTargetMachines = verb.IsEMP();
        var minDistSquared = minDist * minDist;
        var maxLocusDist = maxTravelRadiusFromLocus + verb.EffectiveRange;
        var maxLocusDistSquared = maxLocusDist * maxLocusDist;
        var needNotUnderThickRoof = (flags & TargetScanFlags.NeedNotUnderThickRoof) != TargetScanFlags.None;
        var needLosToAll = (flags & TargetScanFlags.NeedLOSToAll) != TargetScanFlags.None;
        var needLosToPawns = (flags & TargetScanFlags.NeedLOSToPawns) != TargetScanFlags.None;
        var needLosToNonPawns = (flags & TargetScanFlags.NeedLOSToNonPawns) != TargetScanFlags.None;
        var needThreat = (flags & TargetScanFlags.NeedThreat) != TargetScanFlags.None;
        var needAutoTargetable = (flags & TargetScanFlags.NeedAutoTargetable) != TargetScanFlags.None;
        var needActiveThreat = (flags & TargetScanFlags.NeedActiveThreat) != TargetScanFlags.None;
        var needNonBurning = (flags & TargetScanFlags.NeedNonBurning) != TargetScanFlags.None;
        var needReachable = (flags & TargetScanFlags.NeedReachable) != TargetScanFlags.None;
        var needReachableIfCantHit = (flags & TargetScanFlags.NeedReachableIfCantHitFromMyPos) != TargetScanFlags.None;
        var ignoreNonCombatants = (flags & TargetScanFlags.IgnoreNonCombatants) != TargetScanFlags.None;
        var isPlayerSearcher = searcherPawn is { IsColonist: true } || searcherThing.Faction == Faction.OfPlayer;
        var raceProps = searcherThing.def.race;
        var hasIntelligentSearcher = raceProps != null && (int)raceProps.intelligence >= 2;

        Func<IntVec3, bool>? losValidator = null;
        if ((verb.EquipmentSource == null || verb.EquipmentSource.UniqueWeaponComp() is not { } comp ||
             !comp.IgnoreAccuracyMaluses) &&
            (flags & TargetScanFlags.LOSBlockableByGas) != TargetScanFlags.None) {
            losValidator = vec3 => !vec3.AnyGas(searcherThing.Map, GasType.BlindSmoke);
        }

        if ((HasRangedAttack(searcher) || onlyRanged) && (searcherPawn == null || !searcherPawn.InAggroMentalState)) {
            var potentialTargets = searcherThing.Map.attackTargetsCache.GetPotentialTargetsFor(searcher);

            ShootableTargets.Clear();
            FallbackTargets.Clear();

            foreach (var attackTarget in potentialTargets) {
                if (ShouldIgnoreNoncombatant(searcherThing, attackTarget, ignoreNonCombatants)) {
                    continue;
                }

                if (!attackTarget.Thing.Position.InHorDistOf(searcherThing.Position, maxDist)) {
                    continue;
                }

                if (!ValidateTarget(attackTarget, includeDutyRadius: false, includeNoncombatantFilter: false,
                        includeReachability: needReachable)) {
                    continue;
                }

                var canShoot = verb.CanHitTargetFrom(searcherThing.Position, attackTarget.Thing);
                if (canShoot) {
                    ShootableTargets.Add(attackTarget);
                }

                if (!needReachableIfCantHit || needReachable || canShoot ||
                    CanReach(searcherThing, attackTarget.Thing, canBashDoors, canBashFences)) {
                    FallbackTargets.Add(attackTarget);
                }
            }

            if (ShootableTargets.Count > 0) {
                return SelectRandomShootingTargetByScore(ShootableTargets, searcher, verb);
            }

            return FallbackTargets.Count == 0
                ? null
                : (IAttackTarget?)GenClosest.ClosestThing_Global(searcherThing.Position, FallbackTargets, maxDist);
        }

        if (searcherPawn?.mindState.duty is not { radius: > 0f } || searcherPawn.InMentalState) {
            return FindBestMeleeTarget(searcherThing, searcherPawn, FinalValidator, maxDist, canBashDoors,
                canBashFences);
        }

        return FindBestMeleeTarget(searcherThing, searcherPawn, FinalDutyValidator, maxDist, canBashDoors,
            canBashFences);

        bool ValidateTarget(IAttackTarget target, bool includeDutyRadius, bool includeNoncombatantFilter,
            bool includeReachability) {
            var thing = target.Thing;

            if (target == searcher) return false;

            if (minDistSquared > 0f &&
                (searcherThing.Position - thing.Position).LengthHorizontalSquared < minDistSquared) {
                return false;
            }

            if (!canTakeTargetsCloserThanEffectiveMinRange) {
                var effectiveMinRange = verb.verbProps.EffectiveMinRange(thing, searcherThing);
                if (effectiveMinRange > 0f &&
                    (searcherThing.Position - thing.Position).LengthHorizontalSquared <
                    effectiveMinRange * effectiveMinRange) {
                    return false;
                }
            }

            if (maxTravelRadiusFromLocus < 9999f &&
                (thing.Position - locus).LengthHorizontalSquared > maxLocusDistSquared) {
                return false;
            }

            if (!searcherThing.HostileTo(thing)) return false;

            if (validator != null && !validator(thing)) return false;

            var lord = searcherPawn?.GetLord();
            if (lord != null && !lord.LordJob.ValidateAttackTarget(searcherPawn, thing)) {
                return false;
            }

            if (needNotUnderThickRoof) {
                var roof = thing.Position.GetRoof(thing.Map);
                if (roof is { isThickRoof: true }) return false;
            }

            if (needLosToAll) {
                if (losValidator != null && (!losValidator(searcherThing.Position) || !losValidator(thing.Position))) {
                    return false;
                }

                if (!searcherThing.CanSee(thing, losValidator)) {
                    if (target is Pawn) {
                        if (needLosToPawns) return false;
                    } else if (needLosToNonPawns) return false;
                }
            }

            if ((needThreat || needAutoTargetable) && target.ThreatDisabled(searcher)) {
                return false;
            }

            if (needAutoTargetable && !AttackTargetFinder.IsAutoTargetable(target)) {
                return false;
            }

            if (needActiveThreat && !GenHostility.IsActiveThreatTo(target, searcherThing.Faction)) {
                return false;
            }

            if (onlyTargetMachines && target is Pawn machineTarget && machineTarget.RaceProps.IsFlesh) {
                return false;
            }

            if (needNonBurning && thing.IsBurning()) {
                return false;
            }

            if (hasIntelligentSearcher) {
                if (thing is ThingWithComps thingWithComps &&
                    thingWithComps.ExplosiveComp() is { wickStarted: true }) {
                    return false;
                }
            }

            if (isPlayerSearcher) {
                if (thing.def.size is { x: 1, z: 1 }) {
                    if (thing.Position.Fogged(thing.Map)) {
                        return false;
                    }
                } else {
                    var anyVisibleCell = false;
                    foreach (var cell in thing.OccupiedRect()) {
                        if (cell.Fogged(thing.Map)) {
                            continue;
                        }

                        anyVisibleCell = true;
                        break;
                    }

                    if (!anyVisibleCell) {
                        return false;
                    }
                }
            }

            if (includeDutyRadius &&
                !thing.Position.InHorDistOf(searcherPawn!.mindState.duty!.focus.Cell,
                    searcherPawn.mindState.duty.radius)) {
                return false;
            }

            if (includeNoncombatantFilter && ShouldIgnoreNoncombatant(searcherThing, target, ignoreNonCombatants)) {
                return false;
            }

            return !includeReachability || CanReach(searcherThing, thing, canBashDoors, canBashFences);
        }

        bool DutyValidator(IAttackTarget target) => ValidateTarget(target, includeDutyRadius: true,
            includeNoncombatantFilter: false, includeReachability: false);

        bool FinalDutyValidator(IAttackTarget target) => DutyValidator(target) &&
                                                         !ShouldIgnoreNoncombatant(searcherThing, target,
                                                             ignoreNonCombatants);

        bool FinalValidator(IAttackTarget target) =>
            ValidateTarget(target, includeDutyRadius: false, includeNoncombatantFilter: false,
                includeReachability: false) && !ShouldIgnoreNoncombatant(searcherThing, target, ignoreNonCombatants);
    }

    private static IAttackTarget? FindBestMeleeTarget(Thing searcherThing, Pawn? searcherPawn,
        Predicate<IAttackTarget> validator, float maxDist, bool canBashDoors, bool canBashFences) {
        var result = (IAttackTarget?)GenClosest.ClosestThingReachable(
            searcherThing.Position,
            searcherThing.Map,
            ThingRequest.ForGroup(ThingRequestGroup.AttackTarget),
            PathEndMode.Touch,
            TraverseParms.For(searcherPawn, Danger.Deadly, TraverseMode.ByPawn, canBashDoors, false, canBashFences),
            maxDist,
            thing => validator((IAttackTarget)thing),
            null,
            0,
            maxDist > 800f ? -1 : 40
        );

        if (result == null || !PawnUtility.ShouldCollideWithPawns(searcherPawn)) {
            return result;
        }

        var reachableMeleeTarget = _findBestReachableMeleeTarget!(
            validator,
            searcherPawn!,
            maxDist,
            canBashDoors,
            canBashFences
        );

        if (reachableMeleeTarget == null) {
            return result;
        }

        var closestDistance = (searcherPawn!.Position - result.Thing.Position).LengthHorizontal;
        var reachableDistance = (searcherPawn.Position - reachableMeleeTarget.Thing.Position).LengthHorizontal;
        return Mathf.Abs(closestDistance - reachableDistance) < 50f ? reachableMeleeTarget : result;
    }

    private static bool HasRangedAttack(IAttackTargetSearcher searcher) {
        var verb = searcher.CurrentEffectiveVerb;
        return verb != null && !verb.verbProps.IsMeleeAttack;
    }

    private static bool ShouldIgnoreNoncombatant(Thing searcherThing, IAttackTarget target, bool ignoreNonCombatants) {
        if (target is not Pawn pawn) {
            return false;
        }

        if (pawn.IsCombatant()) {
            return false;
        }

        if (ignoreNonCombatants) {
            return true;
        }

        return !GenSight.LineOfSightToThing(searcherThing.Position, pawn, searcherThing.Map);
    }

    private static bool CanReach(Thing searcher, Thing target, bool canBashDoors, bool canBashFences) {
        if (searcher is Pawn pawn) {
            return pawn.CanReach(target, PathEndMode.Touch, Danger.Some, canBashDoors, canBashFences);
        }

        var mode = canBashDoors ? TraverseMode.PassDoors : TraverseMode.NoPassClosedDoors;
        return searcher.Map.reachability.CanReach(searcher.Position, target, PathEndMode.Touch,
            TraverseParms.For(mode));
    }

    private static IAttackTarget? SelectRandomShootingTargetByScore(List<IAttackTarget> targets,
        IAttackTargetSearcher searcher, Verb verb) {
        WeightedTargets.Clear();
        ShootableTargetScores.Clear();

        var bestScore = 0f;
        IAttackTarget? bestTarget = null;

        foreach (var target in targets) {
            if (target == searcher) {
                ShootableTargetScores.Add(float.MinValue);
                continue;
            }

            var score = _getShootingTargetScore!(target, searcher, verb);
            ShootableTargetScores.Add(score);
            if (bestTarget != null && !(score > bestScore)) {
                continue;
            }

            bestTarget = target;
            bestScore = score;
        }

        if (bestScore < 1f) {
            return bestTarget;
        }

        var minAcceptedScore = bestScore - 30f;
        for (var i = 0; i < targets.Count; i++) {
            var target = targets[i];
            if (target == searcher) {
                continue;
            }

            var score = ShootableTargetScores[i];
            if (score < minAcceptedScore) {
                continue;
            }

            WeightedTargets.Add(new Pair<IAttackTarget, float>(
                target,
                Mathf.InverseLerp(minAcceptedScore, bestScore, score)
            ));
        }

        return WeightedTargets.TryRandomElementByWeight(static pair => pair.Second, out var result)
            ? result.First
            : null;
    }

    private static void EnsureDelegatesInitialized() {
        _getShootingTargetScore ??=
            AccessTools.MethodDelegate<GetShootingTargetScoreDelegate>(
                AccessTools.Method(typeof(AttackTargetFinder), "GetShootingTargetScore")!
            );

        _findBestReachableMeleeTarget ??=
            AccessTools.MethodDelegate<FindBestReachableMeleeTargetDelegate>(
                AccessTools.Method(typeof(AttackTargetFinder), "FindBestReachableMeleeTarget")!
            );
    }
}