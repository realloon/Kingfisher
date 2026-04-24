using HarmonyLib;
using JetBrains.Annotations;

// ReSharper disable InconsistentNaming

namespace Kingfisher.Features;

[HarmonyPatch(typeof(Hediff_Shambler), nameof(Hediff_Shambler.TickInterval))]
public static class ShamblerTargetSearchTickIntervalPatch {
    [UsedImplicitly]
    public static void Prefix(Hediff_Shambler __instance, ref int ___nextTargetCheckTick) {
        ShamblerTargetSearchPatch.DeferIdleSearch(__instance.pawn, ref ___nextTargetCheckTick);
    }
}

[HarmonyPatch(typeof(Hediff_Shambler), nameof(Hediff_Shambler.Notify_DelayedAlert))]
public static class ShamblerTargetSearchDelayedAlertPatch {
    [UsedImplicitly]
    public static void Postfix(Hediff_Shambler __instance, float ___alertTimer) {
        ShamblerTargetSearchPatch.NotifyAlertScheduled(__instance.pawn, ___alertTimer);
    }
}

[HarmonyPatch(typeof(Hediff_Shambler), nameof(Hediff_Shambler.PreRemoved))]
public static class ShamblerTargetSearchCleanupPatch {
    [UsedImplicitly]
    public static void Prefix(Hediff_Shambler __instance) {
        ShamblerTargetSearchPatch.Clear(__instance.pawn);
    }
}

#region Helper

internal static class ShamblerTargetSearchPatch {
    private const int IdleProbeBaseTicks = 15000;
    private const int IdleProbeJitterTicks = 2500;
    private const int ActiveSearchGraceTicks = 180;

    private static readonly Dictionary<int, int> WakeUntilTickByPawnId = [];
    private static readonly Dictionary<int, int> NextIdleProbeTickByPawnId = [];

    private static bool IsEngaged(Pawn pawn) => pawn.mindState.enemyTarget != null;

    private static bool IsRecentlyEngaged(Pawn pawn, int currentTick) =>
        currentTick - pawn.mindState.lastEngageTargetTick <= ActiveSearchGraceTicks;

    private static bool IsAlerted(Pawn pawn, int currentTick) =>
        WakeUntilTickByPawnId.TryGetValue(pawn.thingIDNumber, out var wakeUntilTick) && wakeUntilTick > currentTick;

    public static void DeferIdleSearch(Pawn pawn, ref int nextTargetCheckTick) {
        var currentTick = Find.TickManager.TicksGame;
        var pawnId = pawn.thingIDNumber;
        if (IsEngaged(pawn) || IsRecentlyEngaged(pawn, currentTick) || IsAlerted(pawn, currentTick)) {
            NextIdleProbeTickByPawnId.Remove(pawnId);
            return;
        }

        if (currentTick <= nextTargetCheckTick) {
            return;
        }

        if (!NextIdleProbeTickByPawnId.TryGetValue(pawnId, out var nextIdleProbeTick)) {
            nextIdleProbeTick = currentTick + IdleProbeIntervalFor(pawn);
            NextIdleProbeTickByPawnId[pawnId] = nextIdleProbeTick;
        }

        if (currentTick < nextIdleProbeTick) {
            nextTargetCheckTick = nextIdleProbeTick;
            return;
        }

        var nextProbeTick = currentTick + IdleProbeIntervalFor(pawn);
        NextIdleProbeTickByPawnId[pawnId] = nextProbeTick;
        nextTargetCheckTick = nextProbeTick;
    }

    public static void NotifyAlertScheduled(Pawn pawn, float alertTimer) {
        var pawnId = pawn.thingIDNumber;
        WakeUntilTickByPawnId[pawnId] = (int)alertTimer;
        NextIdleProbeTickByPawnId.Remove(pawnId);
    }

    public static void Clear(Pawn pawn) {
        var pawnId = pawn.thingIDNumber;
        WakeUntilTickByPawnId.Remove(pawnId);
        NextIdleProbeTickByPawnId.Remove(pawnId);
    }

    private static int IdleProbeIntervalFor(Pawn pawn) {
        var jitter = Math.Abs(pawn.thingIDNumber % IdleProbeJitterTicks);
        return IdleProbeBaseTicks + jitter;
    }
}

#endregion
