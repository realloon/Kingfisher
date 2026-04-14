#if false
namespace Kingfisher.Profiling.Deep;

// Hotspot investigation toggle:
// Set to true when you need deep hediff profiling again.

[HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.HealthTick))]
public static class DeepHealthTickProfilerPatch {
    [UsedImplicitly]
    public static void Prefix(out long __state) {
        __state = HediffDeepProfiler.BeginScope();
    }

    [UsedImplicitly]
    public static Exception Finalizer(Exception __exception, long __state) {
        HediffDeepProfiler.EndScope(HediffDeepProfiler.Bucket.HealthTick, __state);
        return __exception;
    }
}

[HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.HealthTickInterval))]
public static class DeepHealthTickIntervalProfilerPatch {
    [UsedImplicitly]
    public static void Prefix(out long __state) {
        __state = HediffDeepProfiler.BeginScope();
    }

    [UsedImplicitly]
    public static Exception Finalizer(Exception __exception, long __state) {
        HediffDeepProfiler.EndScope(HediffDeepProfiler.Bucket.HealthTickInterval, __state);
        return __exception;
    }
}

[HarmonyPatch(typeof(ImmunityHandler), "ImmunityHandlerTickInterval")]
public static class ImmunityHandlerProfilerPatch {
    [UsedImplicitly]
    public static void Prefix(out long __state) {
        __state = HediffDeepProfiler.BeginScope();
    }

    [UsedImplicitly]
    public static Exception Finalizer(Exception __exception, long __state) {
        HediffDeepProfiler.EndScope(HediffDeepProfiler.Bucket.ImmunityTickInterval, __state);
        return __exception;
    }
}

[HarmonyPatch(typeof(HediffSet), nameof(HediffSet.DirtyCache))]
public static class HediffSetDirtyCacheProfilerPatch {
    [UsedImplicitly]
    public static void Prefix(out long __state) {
        __state = HediffDeepProfiler.BeginScope();
    }

    [UsedImplicitly]
    public static Exception Finalizer(Exception __exception, long __state) {
        HediffDeepProfiler.EndScope(HediffDeepProfiler.Bucket.DirtyCache, __state);
        return __exception;
    }
}

[HarmonyPatch(typeof(PawnCapacityUtility), nameof(PawnCapacityUtility.CalculateCapacityLevel))]
public static class PawnCapacityUtilityProfilerPatch {
    [UsedImplicitly]
    public static void Prefix(out long __state) {
        __state = HediffDeepProfiler.BeginScope();
    }

    [UsedImplicitly]
    public static Exception Finalizer(Exception __exception, long __state, PawnCapacityDef capacity) {
        HediffDeepProfiler.RecordCapacityRecompute(capacity, __state);
        return __exception;
    }
}

[HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff),
    [typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult)])]
public static class AddHediffProfilerPatch {
    [UsedImplicitly]
    public static void Prefix(Hediff hediff) {
        HediffDeepProfiler.RecordAddHediff(hediff);
    }
}

[HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.RemoveHediff))]
public static class RemoveHediffProfilerPatch {
    [UsedImplicitly]
    public static void Prefix(Hediff hediff) {
        HediffDeepProfiler.RecordRemoveHediff(hediff);
    }
}

[HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
public static class DeepTickManagerProfilerPatch {
    [UsedImplicitly]
    public static void Prefix(out long __state) {
        __state = HediffDeepProfiler.BeginScope();
    }

    [UsedImplicitly]
    public static Exception Finalizer(Exception __exception, long __state) {
        HediffDeepProfiler.EndScope(HediffDeepProfiler.Bucket.GameTick, __state);
        return __exception;
    }

    [UsedImplicitly]
    public static void Postfix() {
        HediffDeepProfiler.NotifySingleTick();
    }
}

[HarmonyPatch]
public static class HediffTickProfilerPatch {
    [UsedImplicitly]
    public static IEnumerable<MethodBase> TargetMethods() =>
        HediffDeepProfiler.TargetMethods(typeof(Hediff), nameof(Hediff.Tick));

    [UsedImplicitly]
    public static void Prefix(out long __state) {
        __state = HediffDeepProfiler.BeginHediffTick();
    }

    [UsedImplicitly]
    public static Exception Finalizer(Exception __exception, long __state, Hediff __instance) {
        HediffDeepProfiler.EndHediffTick(__state, __instance);
        return __exception;
    }
}

[HarmonyPatch]
public static class HediffPostTickProfilerPatch {
    [UsedImplicitly]
    public static IEnumerable<MethodBase> TargetMethods() =>
        HediffDeepProfiler.TargetMethods(typeof(Hediff), nameof(Hediff.PostTick));

    [UsedImplicitly]
    public static void Prefix(out long __state) {
        __state = HediffDeepProfiler.BeginHediffPostTick();
    }

    [UsedImplicitly]
    public static Exception Finalizer(Exception __exception, long __state, Hediff __instance) {
        HediffDeepProfiler.EndHediffPostTick(__state, __instance);
        return __exception;
    }
}

[HarmonyPatch]
public static class HediffTickIntervalProfilerPatch {
    [UsedImplicitly]
    public static IEnumerable<MethodBase> TargetMethods() =>
        HediffDeepProfiler.TargetMethods(typeof(Hediff), nameof(Hediff.TickInterval), typeof(int));

    [UsedImplicitly]
    public static void Prefix(out long __state) {
        __state = HediffDeepProfiler.BeginHediffTickInterval();
    }

    [UsedImplicitly]
    public static Exception Finalizer(Exception __exception, long __state, Hediff __instance) {
        HediffDeepProfiler.EndHediffTickInterval(__state, __instance);
        return __exception;
    }
}

[HarmonyPatch]
public static class HediffPostTickIntervalProfilerPatch {
    [UsedImplicitly]
    public static IEnumerable<MethodBase> TargetMethods() =>
        HediffDeepProfiler.TargetMethods(typeof(Hediff), nameof(Hediff.PostTickInterval), typeof(int));

    [UsedImplicitly]
    public static void Prefix(out long __state) {
        __state = HediffDeepProfiler.BeginHediffPostTickInterval();
    }

    [UsedImplicitly]
    public static Exception Finalizer(Exception __exception, long __state, Hediff __instance) {
        HediffDeepProfiler.EndHediffPostTickInterval(__state, __instance);
        return __exception;
    }
}

[HarmonyPatch]
public static class HediffCompPostTickProfilerPatch {
    private static readonly Type[] CompPostTickArgs = [typeof(float).MakeByRefType()];

    [UsedImplicitly]
    public static IEnumerable<MethodBase> TargetMethods() =>
        HediffDeepProfiler.TargetMethods(typeof(HediffComp), nameof(HediffComp.CompPostTick), CompPostTickArgs);

    [UsedImplicitly]
    public static void Prefix(out long __state) {
        __state = HediffDeepProfiler.BeginHediffCompPostTick();
    }

    [UsedImplicitly]
    public static Exception Finalizer(Exception __exception, long __state, HediffComp __instance) {
        HediffDeepProfiler.EndHediffCompPostTick(__state, __instance);
        return __exception;
    }
}

[HarmonyPatch]
public static class HediffCompPostTickIntervalProfilerPatch {
    private static readonly Type[] CompPostTickIntervalArgs = [typeof(float).MakeByRefType(), typeof(int)];

    [UsedImplicitly]
    public static IEnumerable<MethodBase> TargetMethods() =>
        HediffDeepProfiler.TargetMethods(typeof(HediffComp), nameof(HediffComp.CompPostTickInterval),
            CompPostTickIntervalArgs);

    [UsedImplicitly]
    public static void Prefix(out long __state) {
        __state = HediffDeepProfiler.BeginHediffCompPostTickInterval();
    }

    [UsedImplicitly]
    public static Exception Finalizer(Exception __exception, long __state, HediffComp __instance) {
        HediffDeepProfiler.EndHediffCompPostTickInterval(__state, __instance);
        return __exception;
    }
}

#endif