using JetBrains.Annotations;
using System.Runtime.CompilerServices;
using Kingfisher.Prepatching;

namespace Kingfisher.Features;

public static class WorkGiverTakeToPenRewrite {
    [MethodRewrite(typeof(WorkGiver_TakeToPen), nameof(WorkGiver_TakeToPen.PotentialWorkThingsGlobal))]
    public static IEnumerable<Thing> PotentialWorkThingsGlobal(WorkGiver_TakeToPen giver, Pawn pawn) {
        if (pawn.Faction != Faction.OfPlayer) {
            return EnumerateFactionAnimals(giver, pawn);
        }

        var state = StateByMap.GetOrCreateValue(pawn.Map);
        return state.GetOrBuild(GetCandidateKind(giver), pawn.Map);
    }

    #region Helpers

    private static readonly ConditionalWeakTable<Map, MapState> StateByMap = [];

    private static CandidateKind GetCandidateKind(WorkGiver_TakeToPen giver) {
        return giver switch {
            WorkGiver_RebalanceAnimalsInPens => CandidateKind.Rebalance,
            WorkGiver_TakeRoamingAnimalsToPen => CandidateKind.Roaming,
            _ => CandidateKind.TakeToPen
        };
    }

    private static IEnumerable<Thing> EnumerateFactionAnimals(WorkGiver_TakeToPen giver, Pawn pawn) {
        var candidateKind = GetCandidateKind(giver);
        foreach (var otherPawn in pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction)) {
            if (IsCandidate(otherPawn, candidateKind, pawn.Map)) {
                yield return otherPawn;
            }
        }
    }

    private static bool IsCandidate(Pawn animal, CandidateKind candidateKind, Map map) {
        if (!animal.IsAnimal ||
            !animal.Roamer ||
            !AnimalPenUtility.IsRopeManagedAnimalDef(animal.def) ||
            animal.Map != map) {
            return false;
        }

        if (!MatchesMentalState(animal, candidateKind) ||
            map.designationManager.DesignationOn(animal, DesignationDefOf.ReleaseAnimalToWild) != null) {
            return false;
        }

        if (candidateKind == CandidateKind.Rebalance &&
            map.listerBuildings.allBuildingsAnimalPenMarkers.Count > 1) {
            return true;
        }

        return NeedsImmediatePenWork(animal);
    }

    private static bool MatchesMentalState(Pawn animal, CandidateKind candidateKind) {
        var mentalState = animal.MentalStateDef;
        if (candidateKind == CandidateKind.Roaming) {
            return mentalState == MentalStateDefOf.Roaming;
        }

        return mentalState == null || mentalState == MentalStateDefOf.Roaming;
    }

    private static bool NeedsImmediatePenWork(Pawn animal) {
        if (AnimalPenUtility.IsUnnecessarilyRoped(animal)) {
            return true;
        }

        var currentPen = AnimalPenUtility.GetCurrentPenOf(animal, allowUnenclosedPens: false);
        return currentPen == null || !currentPen.PenState.Enclosed;
    }

    private enum CandidateKind {
        TakeToPen,
        Rebalance,
        Roaming
    }

    [UsedImplicitly]
    private sealed class MapState {
        private readonly CachedCandidates _rebalanceCandidates = new();
        private readonly CachedCandidates _roamingCandidates = new();
        private readonly CachedCandidates _takeToPenCandidates = new();

        public IReadOnlyList<Thing> GetOrBuild(CandidateKind candidateKind, Map map) {
            var cached = candidateKind switch {
                CandidateKind.Rebalance => _rebalanceCandidates,
                CandidateKind.Roaming => _roamingCandidates,
                _ => _takeToPenCandidates
            };

            var currentTick = Find.TickManager.TicksGame;
            if (cached.BuiltAtTick == currentTick) {
                return cached.Candidates;
            }

            cached.BuiltAtTick = currentTick;
            cached.Candidates.Clear();

            foreach (var animal in map.mapPawns.SpawnedColonyAnimals) {
                if (IsCandidate(animal, candidateKind, map)) {
                    cached.Candidates.Add(animal);
                }
            }

            return cached.Candidates;
        }
    }

    private sealed class CachedCandidates {
        public int BuiltAtTick = -1;

        public readonly List<Thing> Candidates = [];
    }

    #endregion
}