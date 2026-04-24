#if DEBUG
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld.Planet;

// ReSharper disable InconsistentNaming

namespace Kingfisher.Profiling.Aggregate;

[HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
public static class AggregateGameTickProbe {
    [UsedImplicitly]
    private static void Prefix(out AggregateProfiler.ScopeState __state) {
        __state = AggregateProfiler.BeginScope(AggregateProfiler.Probe.GameTick);
    }

    [UsedImplicitly]
    private static void Postfix(AggregateProfiler.ScopeState __state) {
        AggregateProfiler.EndScope(__state);
        AggregateProfiler.NotifySingleTick();
    }
}

[HarmonyPatch(typeof(Map), nameof(Map.MapPreTick))]
public static class AggregateMapPreTickProbe {
    [UsedImplicitly]
    private static void Prefix(out AggregateProfiler.ScopeState __state) {
        __state = AggregateProfiler.BeginScope(AggregateProfiler.Probe.MapPreTick);
    }

    [UsedImplicitly]
    private static void Postfix(AggregateProfiler.ScopeState __state) {
        AggregateProfiler.EndScope(__state);
    }
}

[HarmonyPatch(typeof(TickList), nameof(TickList.Tick))]
public static class AggregateTickListProbe {
    [UsedImplicitly]
    private static void Prefix(out AggregateProfiler.ScopeState __state) {
        __state = AggregateProfiler.BeginScope(AggregateProfiler.Probe.TickList);
    }

    [UsedImplicitly]
    private static void Postfix(AggregateProfiler.ScopeState __state) {
        AggregateProfiler.EndScope(__state);
    }
}

[HarmonyPatch(typeof(World), nameof(World.WorldTick))]
public static class AggregateWorldTickProbe {
    [UsedImplicitly]
    private static void Prefix(out AggregateProfiler.ScopeState __state) {
        __state = AggregateProfiler.BeginScope(AggregateProfiler.Probe.WorldTick);
    }

    [UsedImplicitly]
    private static void Postfix(AggregateProfiler.ScopeState __state) {
        AggregateProfiler.EndScope(__state);
    }
}

[HarmonyPatch(typeof(Map), nameof(Map.MapPostTick))]
public static class AggregateMapPostTickProbe {
    [UsedImplicitly]
    private static void Prefix(out AggregateProfiler.ScopeState __state) {
        __state = AggregateProfiler.BeginScope(AggregateProfiler.Probe.MapPostTick);
    }

    [UsedImplicitly]
    private static void Postfix(AggregateProfiler.ScopeState __state) {
        AggregateProfiler.EndScope(__state);
    }
}

[HarmonyPatch(typeof(JobGiver_Work), nameof(JobGiver_Work.TryIssueJobPackage))]
public static class AggregateWorkScanProbe {
    [UsedImplicitly]
    private static void Prefix(out AggregateProfiler.ScopeState __state) {
        __state = AggregateProfiler.BeginScope(AggregateProfiler.Probe.WorkScan);
    }

    [UsedImplicitly]
    private static void Postfix(AggregateProfiler.ScopeState __state) {
        AggregateProfiler.EndScope(__state);
    }
}
#endif