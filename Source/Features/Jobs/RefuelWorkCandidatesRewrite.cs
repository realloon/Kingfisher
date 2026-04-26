using JetBrains.Annotations;
using HarmonyLib;
using Prepatcher;
using Kingfisher.Prepatching;

// ReSharper disable InconsistentNaming

namespace Kingfisher.Features;

public static class RefuelWorkCandidatesRewrite {
    [MethodRewrite(typeof(WorkGiver_Scanner), nameof(WorkGiver_Scanner.PotentialWorkThingsGlobal))]
    public static IEnumerable<Thing>? PotentialWorkThingsGlobal(WorkGiver_Scanner scanner, Pawn pawn) {
        return scanner is WorkGiver_Refuel ? RefuelWorkCandidates.CandidatesFor(pawn.Map) : null;
    }
}

[HarmonyPatch(typeof(CompRefuelable), nameof(CompRefuelable.PostSpawnSetup))]
public static class RefuelableSpawnPatch {
    [UsedImplicitly]
    public static void Postfix(CompRefuelable __instance) {
        RefuelWorkCandidates.NotifyPotentialStateChanged(__instance);
    }
}

[HarmonyPatch(typeof(ThingComp), nameof(ThingComp.PostDeSpawn))]
public static class RefuelableDeSpawnPatch {
    [UsedImplicitly]
    public static void Prefix(ThingComp __instance, Map map) {
        if (__instance is CompRefuelable refuelable) {
            RefuelWorkCandidates.NotifyRemoved(refuelable, map);
        }
    }
}

[HarmonyPatch(typeof(CompRefuelable), nameof(CompRefuelable.PostDestroy))]
public static class RefuelableDestroyPatch {
    [UsedImplicitly]
    public static void Prefix(CompRefuelable __instance, Map previousMap) {
        RefuelWorkCandidates.NotifyRemoved(__instance, previousMap);
    }
}

[HarmonyPatch(typeof(CompRefuelable), nameof(CompRefuelable.ConsumeFuel))]
public static class RefuelableConsumeFuelPatch {
    [UsedImplicitly]
    public static void Postfix(CompRefuelable __instance) {
        RefuelWorkCandidates.NotifyFuelConsumed(__instance);
    }
}

[HarmonyPatch(typeof(CompRefuelable), nameof(CompRefuelable.Refuel), [typeof(float)])]
public static class RefuelableRefuelAmountPatch {
    [UsedImplicitly]
    public static void Postfix(CompRefuelable __instance) {
        RefuelWorkCandidates.NotifyPotentialStateChanged(__instance);
    }
}

[HarmonyPatch(typeof(CompRefuelable), nameof(CompRefuelable.Refuel), [typeof(List<Thing>)])]
public static class RefuelableRefuelThingsPatch {
    [UsedImplicitly]
    public static void Postfix(CompRefuelable __instance) {
        RefuelWorkCandidates.NotifyPotentialStateChanged(__instance);
    }
}

#region Helper

public static class RefuelWorkCandidates {
    private const int RebuildIntervalTicks = 250;

    public static List<Thing> CandidatesFor(Map map) {
        var currentTick = CurrentTick();
        var state = map.RefuelWorkCandidatesState();
        if (currentTick < state.LastRebuildTick || currentTick - state.LastRebuildTick >= RebuildIntervalTicks) {
            Rebuild(map, state, currentTick);
        }

        return state.Candidates;
    }

    public static void NotifyPotentialStateChanged(CompRefuelable comp) {
        var parent = comp.parent;
        if (parent?.Spawned != true) {
            return;
        }

        var state = parent.Map.RefuelWorkCandidatesState();
        UpdateCandidate(parent, comp, state);
    }

    public static void NotifyFuelConsumed(CompRefuelable comp) {
        var parent = comp.parent;
        if (parent?.Spawned != true || comp.FuelPercentOfTarget > comp.Props.autoRefuelPercent) {
            return;
        }

        var state = parent.Map.RefuelWorkCandidatesState();
        UpdateCandidate(parent, comp, state);
    }

    public static void NotifyRemoved(CompRefuelable comp, Map? map) {
        if (map == null) {
            return;
        }

        var parent = comp.parent;
        var state = map.RefuelWorkCandidatesState();
        if (state.CandidateSet.Remove(parent)) {
            state.Candidates.Remove(parent);
        }
    }

    private static void Rebuild(Map map, State state, int currentTick) {
        state.CandidateSet.Clear();
        state.Candidates.Clear();

        var refuelables = map.listerThings.ThingsInGroup(ThingRequestGroup.Refuelable);
        foreach (var thing in refuelables) {
            var comp = thing.TryGetComp<CompRefuelable>();
            if (comp == null) {
                continue;
            }

            if (!PotentiallyNeedsAutoRefuel(thing, comp)) continue;

            state.CandidateSet.Add(thing);
            state.Candidates.Add(thing);
        }

        state.LastRebuildTick = currentTick;
    }

    private static void UpdateCandidate(Thing thing, CompRefuelable comp, State state) {
        if (PotentiallyNeedsAutoRefuel(thing, comp)) {
            if (state.CandidateSet.Add(thing)) {
                state.Candidates.Add(thing);
            }

            return;
        }

        if (state.CandidateSet.Remove(thing)) {
            state.Candidates.Remove(thing);
        }
    }

    private static bool PotentiallyNeedsAutoRefuel(Thing thing, CompRefuelable comp) {
        if (thing is Building_Turret || !thing.Spawned || thing.Fogged()) {
            return false;
        }

        if (!comp.allowAutoRefuel) {
            return false;
        }

        if (comp.FuelPercentOfMax > 0f && !comp.Props.allowRefuelIfNotEmpty) {
            return false;
        }

        return comp.ShouldAutoRefuelNow;
    }

    private static int CurrentTick() => Find.TickManager.TicksGame;

    private sealed class State {
        public readonly HashSet<Thing> CandidateSet = [];
        public readonly List<Thing> Candidates = [];
        public int LastRebuildTick = -99999;
    }

    [PrepatcherField]
    [ValueInitializer(nameof(CreateState))]
    private static extern State RefuelWorkCandidatesState(this Map target);

    private static State CreateState() => new();
}

#endregion