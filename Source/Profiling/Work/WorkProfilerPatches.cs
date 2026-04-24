#if DEBUG
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using Verse.AI;

// ReSharper disable InconsistentNaming

namespace Kingfisher.Profiling.Work;

[HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
public static class WorkProfilerTickManagerPatch {
    [UsedImplicitly]
    private static void Postfix() {
        WorkProfiler.NotifySingleTick();
    }
}

[HarmonyPatch(typeof(JobGiver_Work), nameof(JobGiver_Work.TryIssueJobPackage))]
public static class WorkProfilerTryIssueJobPackagePatch {
    [UsedImplicitly]
    private static void Prefix(Pawn pawn, out WorkProfiler.WorkScanState __state) {
        __state = WorkProfiler.BeginWorkScan(pawn);
    }

    [UsedImplicitly]
    private static void Postfix(ThinkResult __result, WorkProfiler.WorkScanState __state) {
        WorkProfiler.EndWorkScan(__state, __result);
    }
}

[HarmonyPatch(typeof(JobGiver_Work), "PawnCanUseWorkGiver")]
public static class WorkProfilerPawnCanUseWorkGiverPatch {
    [UsedImplicitly]
    private static void Prefix(WorkGiver giver, out WorkProfiler.ScopeState __state) {
        __state = WorkProfiler.BeginProbe(WorkProfiler.Probe.PawnCanUseWorkGiver, giver);
    }

    [UsedImplicitly]
    private static void Postfix(WorkProfiler.ScopeState __state) {
        WorkProfiler.EndProbe(__state);
    }
}

[HarmonyPatch]
public static class WorkProfilerNonScanJobPatch {
    [UsedImplicitly]
    private static IEnumerable<MethodBase> TargetMethods() =>
        WorkProfilerPatchTargets.GetVirtualTargets(typeof(WorkGiver), nameof(WorkGiver.NonScanJob), [typeof(Pawn)]);

    [UsedImplicitly]
    private static void Prefix(WorkGiver __instance, out WorkProfiler.ScopeState __state) {
        __state = WorkProfiler.BeginProbe(WorkProfiler.Probe.NonScanJob, __instance);
    }

    [UsedImplicitly]
    private static void Postfix(WorkProfiler.ScopeState __state) {
        WorkProfiler.EndProbe(__state);
    }
}

[HarmonyPatch]
public static class WorkProfilerPotentialWorkThingsGlobalPatch {
    [UsedImplicitly]
    private static IEnumerable<MethodBase> TargetMethods() => WorkProfilerPatchTargets.GetVirtualTargets(
        typeof(WorkGiver_Scanner),
        nameof(WorkGiver_Scanner.PotentialWorkThingsGlobal),
        [typeof(Pawn)]);

    [UsedImplicitly]
    private static void Prefix(WorkGiver_Scanner __instance, out WorkProfiler.ScopeState __state) {
        __state = WorkProfiler.BeginProbe(WorkProfiler.Probe.PotentialWorkThingsGlobal, __instance);
    }

    [UsedImplicitly]
    private static void Postfix(WorkProfiler.ScopeState __state) {
        WorkProfiler.EndProbe(__state);
    }
}

[HarmonyPatch]
public static class WorkProfilerPotentialWorkCellsGlobalPatch {
    [UsedImplicitly]
    private static IEnumerable<MethodBase> TargetMethods() => WorkProfilerPatchTargets.GetVirtualTargets(
        typeof(WorkGiver_Scanner),
        nameof(WorkGiver_Scanner.PotentialWorkCellsGlobal),
        [typeof(Pawn)]);

    [UsedImplicitly]
    private static void Prefix(WorkGiver_Scanner __instance, out WorkProfiler.ScopeState __state) {
        __state = WorkProfiler.BeginProbe(WorkProfiler.Probe.PotentialWorkCellsGlobal, __instance);
    }

    [UsedImplicitly]
    private static void Postfix(WorkProfiler.ScopeState __state) {
        WorkProfiler.EndProbe(__state);
    }
}

[HarmonyPatch]
public static class WorkProfilerHasJobOnThingPatch {
    [UsedImplicitly]
    private static IEnumerable<MethodBase> TargetMethods() =>
        WorkProfilerPatchTargets.GetVirtualTargets(
            typeof(WorkGiver_Scanner),
            nameof(WorkGiver_Scanner.HasJobOnThing),
            [typeof(Pawn), typeof(Thing), typeof(bool)]);

    [UsedImplicitly]
    private static void Prefix(WorkGiver_Scanner __instance, out WorkProfiler.ScopeState __state) {
        __state = WorkProfiler.BeginProbe(WorkProfiler.Probe.HasJobOnThing, __instance);
    }

    [UsedImplicitly]
    private static void Postfix(WorkProfiler.ScopeState __state) {
        WorkProfiler.EndProbe(__state);
    }
}

[HarmonyPatch]
public static class WorkProfilerHasJobOnCellPatch {
    [UsedImplicitly]
    private static IEnumerable<MethodBase> TargetMethods() =>
        WorkProfilerPatchTargets.GetVirtualTargets(
            typeof(WorkGiver_Scanner),
            nameof(WorkGiver_Scanner.HasJobOnCell),
            [typeof(Pawn), typeof(IntVec3), typeof(bool)]);

    [UsedImplicitly]
    private static void Prefix(WorkGiver_Scanner __instance, out WorkProfiler.ScopeState __state) {
        __state = WorkProfiler.BeginProbe(WorkProfiler.Probe.HasJobOnCell, __instance);
    }

    [UsedImplicitly]
    private static void Postfix(WorkProfiler.ScopeState __state) {
        WorkProfiler.EndProbe(__state);
    }
}

[HarmonyPatch]
public static class WorkProfilerJobOnThingPatch {
    [UsedImplicitly]
    private static IEnumerable<MethodBase> TargetMethods() =>
        WorkProfilerPatchTargets.GetVirtualTargets(
            typeof(WorkGiver_Scanner),
            nameof(WorkGiver_Scanner.JobOnThing),
            [typeof(Pawn), typeof(Thing), typeof(bool)]);

    [UsedImplicitly]
    private static void Prefix(WorkGiver_Scanner __instance, out WorkProfiler.ScopeState __state) {
        __state = WorkProfiler.BeginProbe(WorkProfiler.Probe.JobOnThing, __instance);
    }

    [UsedImplicitly]
    private static void Postfix(WorkProfiler.ScopeState __state) {
        WorkProfiler.EndProbe(__state);
    }
}

[HarmonyPatch]
public static class WorkProfilerJobOnCellPatch {
    [UsedImplicitly]
    private static IEnumerable<MethodBase> TargetMethods() =>
        WorkProfilerPatchTargets.GetVirtualTargets(
            typeof(WorkGiver_Scanner),
            nameof(WorkGiver_Scanner.JobOnCell),
            [typeof(Pawn), typeof(IntVec3), typeof(bool)]);

    [UsedImplicitly]
    private static void Prefix(WorkGiver_Scanner __instance, out WorkProfiler.ScopeState __state) {
        __state = WorkProfiler.BeginProbe(WorkProfiler.Probe.JobOnCell, __instance);
    }

    [UsedImplicitly]
    private static void Postfix(WorkProfiler.ScopeState __state) {
        WorkProfiler.EndProbe(__state);
    }
}

[HarmonyPatch]
public static class WorkProfilerGetPriorityPatch {
    [UsedImplicitly]
    private static IEnumerable<MethodBase> TargetMethods() =>
        WorkProfilerPatchTargets.GetVirtualTargets(
            typeof(WorkGiver_Scanner),
            nameof(WorkGiver_Scanner.GetPriority),
            [typeof(Pawn), typeof(TargetInfo)]);

    [UsedImplicitly]
    private static void Prefix(WorkGiver_Scanner __instance, out WorkProfiler.ScopeState __state) {
        __state = WorkProfiler.BeginProbe(WorkProfiler.Probe.GetPriority, __instance);
    }

    [UsedImplicitly]
    private static void Postfix(WorkProfiler.ScopeState __state) {
        WorkProfiler.EndProbe(__state);
    }
}

[HarmonyPatch(typeof(GenClosest), nameof(GenClosest.ClosestThing_Global))]
public static class WorkProfilerClosestThingGlobalPatch {
    [UsedImplicitly]
    private static void Prefix(out WorkProfiler.ScopeState __state) {
        __state = WorkProfiler.BeginProbe(WorkProfiler.Probe.ClosestThingGlobal, null);
    }

    [UsedImplicitly]
    private static void Postfix(WorkProfiler.ScopeState __state) {
        WorkProfiler.EndProbe(__state);
    }
}

[HarmonyPatch(typeof(GenClosest), nameof(GenClosest.ClosestThing_Global_Reachable))]
public static class WorkProfilerClosestThingGlobalReachablePatch {
    [UsedImplicitly]
    private static void Prefix(out WorkProfiler.ScopeState __state) {
        __state = WorkProfiler.BeginProbe(WorkProfiler.Probe.ClosestThingGlobalReachable, null);
    }

    [UsedImplicitly]
    private static void Postfix(WorkProfiler.ScopeState __state) {
        WorkProfiler.EndProbe(__state);
    }
}

[HarmonyPatch(typeof(GenClosest), nameof(GenClosest.ClosestThingReachable))]
public static class WorkProfilerClosestThingReachablePatch {
    [UsedImplicitly]
    private static void Prefix(out WorkProfiler.ScopeState __state) {
        __state = WorkProfiler.BeginProbe(WorkProfiler.Probe.ClosestThingReachable, null);
    }

    [UsedImplicitly]
    private static void Postfix(WorkProfiler.ScopeState __state) {
        WorkProfiler.EndProbe(__state);
    }
}

[HarmonyPatch(typeof(ReachabilityUtility), nameof(ReachabilityUtility.CanReach),
    [typeof(Pawn), typeof(LocalTargetInfo), typeof(PathEndMode), typeof(Danger), typeof(bool), typeof(bool),
        typeof(TraverseMode)])]
public static class WorkProfilerPawnCanReachPatch {
    [UsedImplicitly]
    private static void Prefix(out WorkProfiler.ScopeState __state) {
        __state = WorkProfiler.BeginProbe(WorkProfiler.Probe.PawnCanReach, null);
    }

    [UsedImplicitly]
    private static void Postfix(WorkProfiler.ScopeState __state) {
        WorkProfiler.EndProbe(__state);
    }
}

public static class WorkProfilerPatchTargets {
    public static IEnumerable<MethodBase> GetVirtualTargets(Type baseType, string methodName, Type[] parameterTypes) {
        var baseMethod = AccessTools.Method(baseType, methodName, parameterTypes);
        if (baseMethod != null) {
            yield return baseMethod;
        }

        foreach (var type in AccessTools.AllTypes()) {
            if (type == null || type.IsAbstract || !baseType.IsAssignableFrom(type) || type == baseType) {
                continue;
            }

            var method = AccessTools.DeclaredMethod(type, methodName, parameterTypes);
            if (method != null) {
                yield return method;
            }
        }
    }
}
#endif
