using HarmonyLib;
using JetBrains.Annotations;
using RimWorld.Planet;

// ReSharper disable InconsistentNaming

namespace Kingfisher.Profiling.Aggregate;

[HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
internal static class AggregateGameTickProbe {
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
internal static class AggregateMapPreTickProbe {
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
internal static class AggregateTickListProbe {
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
internal static class AggregateWorldTickProbe {
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
internal static class AggregateMapPostTickProbe {
    [UsedImplicitly]
    private static void Prefix(out AggregateProfiler.ScopeState __state) {
        __state = AggregateProfiler.BeginScope(AggregateProfiler.Probe.MapPostTick);
    }

    [UsedImplicitly]
    private static void Postfix(AggregateProfiler.ScopeState __state) {
        AggregateProfiler.EndScope(__state);
    }
}
