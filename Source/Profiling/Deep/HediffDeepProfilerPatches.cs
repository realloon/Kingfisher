#if false
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;

// ReSharper disable InconsistentNaming

namespace Kingfisher.Profiling.Deep;

// Hotspot investigation toggle:
// Set to true when you need targeted hediff profiling again.

[HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
public static class HediffDeepProfilerTickManagerPatch {
    [UsedImplicitly]
    public static void Postfix() {
        HediffDeepProfiler.NotifySingleTick();
    }
}

[HarmonyPatch(typeof(ImmunityHandler), "NeededImmunitiesNow")]
public static class NeededImmunitiesNowProfilerPatch {
    [UsedImplicitly]
    public static void Prefix(out long __state) {
        __state = HediffDeepProfiler.BeginScope();
    }

    [UsedImplicitly]
    public static Exception Finalizer(Exception __exception, long __state, ImmunityHandler __instance,
        List<ImmunityHandler.ImmunityInfo> __result) {
        HediffDeepProfiler.EndNeededImmunitiesNow(__state, __instance, __result);
        return __exception;
    }
}

[HarmonyPatch(typeof(ImmunityHandler), nameof(ImmunityHandler.TryAddImmunityRecord))]
public static class TryAddImmunityRecordProfilerPatch {
    public struct State {
        public long StartTimestamp;
        public int ImmunityCountBefore;
    }

    [UsedImplicitly]
    public static void Prefix(ImmunityHandler __instance, out State __state) {
        __state = new State {
            StartTimestamp = HediffDeepProfiler.BeginScope(),
            ImmunityCountBefore = HediffDeepProfiler.GetImmunityRecordCount(__instance)
        };
    }

    [UsedImplicitly]
    public static Exception Finalizer(Exception __exception, State __state,
        ImmunityHandler __instance, HediffDef def) {
        HediffDeepProfiler.EndTryAddImmunityRecord(__state.StartTimestamp, __state.ImmunityCountBefore, __instance,
            def);
        return __exception;
    }
}

[HarmonyPatch(typeof(ImmunityHandler), nameof(ImmunityHandler.ImmunityRecordExists))]
public static class ImmunityRecordExistsProfilerPatch {
    [UsedImplicitly]
    public static void Prefix(out long __state) {
        __state = HediffDeepProfiler.BeginScope();
    }

    [UsedImplicitly]
    public static Exception Finalizer(Exception __exception, long __state, HediffDef def, bool __result) {
        HediffDeepProfiler.EndImmunityRecordExists(__state, def, __result);
        return __exception;
    }
}

[HarmonyPatch(typeof(HediffUtility), nameof(HediffUtility.IsTended))]
public static class HediffUtilityIsTendedProfilerPatch {
    [UsedImplicitly]
    public static void Prefix(out long __state) {
        __state = HediffDeepProfiler.BeginScope();
    }

    [UsedImplicitly]
    public static Exception Finalizer(Exception __exception, long __state, Hediff hd) {
        HediffDeepProfiler.EndTryGetComp(__state, typeof(HediffComp_TendDuration),
            HediffDeepProfiler.HasComp(hd, typeof(HediffComp_TendDuration)));
        return __exception;
    }
}

[HarmonyPatch(typeof(HediffUtility), nameof(HediffUtility.IsPermanent))]
public static class HediffUtilityIsPermanentProfilerPatch {
    [UsedImplicitly]
    public static void Prefix(out long __state) {
        __state = HediffDeepProfiler.BeginScope();
    }

    [UsedImplicitly]
    public static Exception Finalizer(Exception __exception, long __state, Hediff hd) {
        HediffDeepProfiler.EndTryGetComp(__state, typeof(HediffComp_GetsPermanent),
            HediffDeepProfiler.HasComp(hd, typeof(HediffComp_GetsPermanent)));
        return __exception;
    }
}

[HarmonyPatch(typeof(HediffUtility), nameof(HediffUtility.FullyImmune))]
public static class HediffUtilityFullyImmuneProfilerPatch {
    [UsedImplicitly]
    public static void Prefix(out long __state) {
        __state = HediffDeepProfiler.BeginScope();
    }

    [UsedImplicitly]
    public static Exception Finalizer(Exception __exception, long __state, Hediff hd) {
        HediffDeepProfiler.EndTryGetComp(__state, typeof(HediffComp_Immunizable),
            HediffDeepProfiler.HasComp(hd, typeof(HediffComp_Immunizable)));
        return __exception;
    }
}

[HarmonyPatch(typeof(ImmunityRecord), nameof(ImmunityRecord.ImmunityChangePerTick))]
public static class ImmunityChangePerTickProfilerPatch {
    [UsedImplicitly]
    public static void Prefix(out long __state) {
        __state = HediffDeepProfiler.BeginScope();
    }

    [UsedImplicitly]
    public static Exception Finalizer(Exception __exception, long __state, ImmunityRecord __instance) {
        HediffDeepProfiler.EndImmunityChangePerTick(__state, __instance.hediffDef);
        return __exception;
    }
}
#endif
