using HarmonyLib;
using JetBrains.Annotations;

// ReSharper disable InconsistentNaming

namespace Kingfisher.Profiling.Aggregate;

[HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.HealthTick))]
public static class AggregateHealthTickProbe {
    [UsedImplicitly]
    public static void Prefix(out long __state) {
        __state = AggregateProfiler.BeginScope();
    }

    [UsedImplicitly]
    public static Exception Finalizer(Exception __exception, long __state) {
        AggregateProfiler.EndScope(AggregateProfiler.Probe.HealthTick, __state);
        return __exception;
    }
}

[HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.HealthTickInterval))]
public static class AggregateHealthTickIntervalProbe {
    [UsedImplicitly]
    public static void Prefix(out long __state) {
        __state = AggregateProfiler.BeginScope();
    }

    [UsedImplicitly]
    public static Exception Finalizer(Exception __exception, long __state) {
        AggregateProfiler.EndScope(AggregateProfiler.Probe.HealthTickInterval, __state);
        return __exception;
    }
}

[HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
public static class AggregateGameTickProbe {
    [UsedImplicitly]
    public static void Prefix(out long __state) {
        __state = AggregateProfiler.BeginScope();
    }

    [UsedImplicitly]
    public static Exception Finalizer(Exception __exception, long __state) {
        AggregateProfiler.EndScope(AggregateProfiler.Probe.GameTick, __state);
        return __exception;
    }

    [UsedImplicitly]
    public static void Postfix() {
        AggregateProfiler.NotifySingleTick();
    }
}
