#if false
using JetBrains.Annotations;
using System.Runtime.CompilerServices;
using Kingfisher.Prepatching;

namespace Kingfisher.Features;

public static class WorkGiverDoBillRewrite {
    [MethodRewrite(typeof(WorkGiver_DoBill), nameof(WorkGiver_DoBill.ShouldSkip))]
    public static bool ShouldSkip(WorkGiver_DoBill workGiver, Pawn pawn, bool _ = false) {
        var activeBillGivers = GetPotentialBillGivers(workGiver, pawn);

        foreach (var thing in activeBillGivers) {
            if (thing != pawn) return false;
        }

        return true;
    }

    #region Helpers

    private static readonly ConditionalWeakTable<Map, MapState> StateByMap = [];

    public static IReadOnlyList<Thing> GetPotentialBillGivers(WorkGiver_DoBill workGiver, Pawn pawn) {
        var state = StateByMap.GetOrCreateValue(pawn.Map);
        return state.GetOrBuild(workGiver, pawn.Map);
    }

    [UsedImplicitly]
    private sealed class MapState {
        private readonly Dictionary<WorkGiverDef, CachedActiveGivers> _activeBillGiversByWorkGiver = [];

        public IReadOnlyList<Thing> GetOrBuild(WorkGiver_DoBill workGiver, Map map) {
            if (!_activeBillGiversByWorkGiver.TryGetValue(workGiver.def, out var cached)) {
                cached = new CachedActiveGivers();
                _activeBillGiversByWorkGiver.Add(workGiver.def, cached);
            }

            var currentTick = Find.TickManager.TicksGame;
            if (cached.BuiltAtTick == currentTick) {
                return cached.ActiveBillGivers;
            }

            cached.BuiltAtTick = currentTick;
            cached.ActiveBillGivers.Clear();

            var request = workGiver.PotentialWorkThingRequest;
            var source = request.singleDef != null
                ? map.listerThings.ThingsOfDef(request.singleDef)
                : map.listerThings.ThingsInGroup(ThingRequestGroup.PotentialBillGiver);

            foreach (var thing in source) {
                if (thing is not IBillGiver billGiver ||
                    !workGiver.ThingIsUsableBillGiver(thing) ||
                    !billGiver.BillStack.AnyShouldDoNow) {
                    continue;
                }

                cached.ActiveBillGivers.Add(thing);
            }

            return cached.ActiveBillGivers;
        }
    }

    private sealed class CachedActiveGivers {
        public int BuiltAtTick = -1;
        public readonly List<Thing> ActiveBillGivers = [];
    }

    #endregion
}
#endif