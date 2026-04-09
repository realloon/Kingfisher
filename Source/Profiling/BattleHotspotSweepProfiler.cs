#if DEBUG
using System.Diagnostics;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;
using HarmonyLib;
using Verse.AI;

// ReSharper disable InconsistentNaming

namespace Kingfisher.Profiling;

internal static class BattleHotspotSweepProfiler {
    private const int FlushIntervalTicks = 600;
    private const string Prefix = "[KF DEV]";

    private static readonly AccessTools.FieldRef<Explosion, List<IntVec3>> CellsToAffectRef =
        AccessTools.FieldRefAccess<Explosion, List<IntVec3>>("cellsToAffect");

    private static readonly AccessTools.FieldRef<Pawn_PathFollower, Pawn> PawnPathFollowerPawnRef =
        AccessTools.FieldRefAccess<Pawn_PathFollower, Pawn>("pawn");

    private static readonly FieldInfo MessageQueueField = AccessTools.Field(typeof(Log), "messageQueue");
    private static readonly FieldInfo LogLockField = AccessTools.Field(typeof(Log), "logLock");
    private static readonly MethodInfo PostMessageMethod = AccessTools.Method(typeof(Log), "PostMessage");

    private static int _windowStartTick = -1;

    private static Metric _attackTargetsCacheGetPotentialTargetsFor;
    private static Metric _pawnPathFollowerPatherTick;
    private static Metric _pawnStanceTrackerTick;
    private static Metric _projectileTickInterval;
    private static Metric _thingTakeDamage;
    private static Metric _explosionTick;
    private static Metric _thingDeSpawn;

    public static bool TryStart(out long startTimestamp) {
        startTimestamp = -1L;

        if (Current.ProgramState != ProgramState.Playing || Find.TickManager == null) {
            return false;
        }

        var currentTick = Find.TickManager.TicksGame;
        if (currentTick < 0) {
            return false;
        }

        FlushIfNeeded(currentTick);

        if (_windowStartTick < 0) {
            _windowStartTick = currentTick;
        }

        startTimestamp = Stopwatch.GetTimestamp();
        return true;
    }

    public static void RecordAttackTargetsCacheGetPotentialTargetsFor(long startTimestamp,
        IAttackTargetSearcher searcher, List<IAttackTarget> result) {
        if (startTimestamp < 0L) {
            return;
        }

        _attackTargetsCacheGetPotentialTargetsFor.Record(
            startTimestamp,
            flagA: searcher is Pawn,
            flagB: searcher.Thing.Faction == Faction.OfPlayer,
            extraMetric: result.Count
        );
    }

    public static void RecordPawnPathFollowerPatherTick(long startTimestamp, Pawn_PathFollower pathFollower) {
        if (startTimestamp < 0L) {
            return;
        }

        var pawn = PawnPathFollowerPawnRef(pathFollower);
        _pawnPathFollowerPatherTick.Record(
            startTimestamp,
            flagA: pathFollower.Moving,
            flagB: pawn is { Drafted: true }
        );
    }

    public static void RecordPawnStanceTrackerTick(long startTimestamp, Pawn_StanceTracker stanceTracker) {
        if (startTimestamp < 0L) {
            return;
        }

        _pawnStanceTrackerTick.Record(
            startTimestamp,
            flagA: stanceTracker.curStance is Stance_Warmup,
            flagB: stanceTracker.FullBodyBusy
        );
    }

    public static void RecordProjectileTickInterval(long startTimestamp, Projectile projectile) {
        if (startTimestamp < 0L) {
            return;
        }

        _projectileTickInterval.Record(
            startTimestamp,
            flagA: projectile is Projectile_Explosive,
            flagB: projectile.Launcher is Pawn
        );
    }

    public static void RecordThingTakeDamage(long startTimestamp, Thing thing, DamageInfo dinfo) {
        if (startTimestamp < 0L) return;

        _thingTakeDamage.Record(
            startTimestamp,
            flagA: thing is Pawn,
            flagB: dinfo.Def.harmsHealth
        );
    }

    public static ExplosionTickState CreateExplosionTickState(long startTimestamp, Explosion explosion) {
        return new ExplosionTickState(
            startTimestamp,
            explosion.instigator is Projectile,
            GetPendingCellsCount(explosion)
        );
    }

    public static void RecordExplosionTick(ExplosionTickState state, Explosion explosion) {
        if (state.StartTimestamp < 0L) {
            return;
        }

        _explosionTick.Record(
            state.StartTimestamp,
            flagA: state.ProjectileInstigator,
            flagB: GetPendingCellsCount(explosion) == 0,
            extraMetric: state.PendingCellsBefore
        );
    }

    private static int GetPendingCellsCount(Explosion explosion) {
        var cellsToAffect = CellsToAffectRef(explosion);
        return cellsToAffect?.Count ?? 0;
    }

    public static void RecordThingDeSpawn(long startTimestamp, Thing thing) {
        if (startTimestamp < 0L) {
            return;
        }

        _thingDeSpawn.Record(
            startTimestamp,
            flagA: thing.def.projectile != null,
            flagB: thing.def.category == ThingCategory.Item
        );
    }

    private static void FlushIfNeeded(int currentTick) {
        if (_windowStartTick < 0) {
            _windowStartTick = currentTick;
            return;
        }

        if (currentTick - _windowStartTick < FlushIntervalTicks) {
            return;
        }

        Flush(currentTick - 1);
        Reset(currentTick);
    }

    private static void Flush(int endTick) {
        LogMetric("AttackTargetsCache.GetPotentialTargetsFor", _attackTargetsCacheGetPotentialTargetsFor, endTick,
            "pawnSearcherCalls", _attackTargetsCacheGetPotentialTargetsFor.FlagACalls,
            "playerFactionSearcherCalls", _attackTargetsCacheGetPotentialTargetsFor.FlagBCalls,
            "avgTargets", _attackTargetsCacheGetPotentialTargetsFor.AverageExtraMetric);

        LogMetric("Pawn_PathFollower.PatherTick", _pawnPathFollowerPatherTick, endTick,
            "movingCalls", _pawnPathFollowerPatherTick.FlagACalls,
            "draftedCalls", _pawnPathFollowerPatherTick.FlagBCalls);

        LogMetric("Pawn_StanceTracker.StanceTrackerTick", _pawnStanceTrackerTick, endTick,
            "warmupCalls", _pawnStanceTrackerTick.FlagACalls,
            "fullBodyBusyCalls", _pawnStanceTrackerTick.FlagBCalls);

        LogMetric("Projectile.TickInterval", _projectileTickInterval, endTick,
            "explosiveCalls", _projectileTickInterval.FlagACalls,
            "pawnLauncherCalls", _projectileTickInterval.FlagBCalls);

        LogMetric("Thing.TakeDamage", _thingTakeDamage, endTick,
            "pawnCalls", _thingTakeDamage.FlagACalls,
            "harmsHealthCalls", _thingTakeDamage.FlagBCalls);

        LogMetric("Explosion.Tick", _explosionTick, endTick,
            "projectileInstigatorCalls", _explosionTick.FlagACalls,
            "finishedCalls", _explosionTick.FlagBCalls,
            "avgPendingCellsBefore", _explosionTick.AverageExtraMetric);

        LogMetric("Thing.DeSpawn", _thingDeSpawn, endTick,
            "projectileCalls", _thingDeSpawn.FlagACalls,
            "itemCalls", _thingDeSpawn.FlagBCalls);
    }

    private static void LogMetric(string name, Metric metric, int endTick, string label1, long value1, string label2,
        long value2, string? extraLabel = null, float extraValue = 0f) {
        if (metric.Calls == 0) {
            return;
        }

        var builder = new StringBuilder(256);
        builder.Append(Prefix)
            .Append(' ')
            .Append("BattleHotspotSweep.")
            .Append(name)
            .Append(" ticks=")
            .Append(_windowStartTick)
            .Append('-')
            .Append(endTick)
            .Append(" calls=")
            .Append(metric.Calls)
            .Append(" totalMs=")
            .Append(metric.TotalMilliseconds.ToString("0.###"))
            .Append(" avgUs=")
            .Append(metric.AverageMicroseconds.ToString("0.###"))
            .Append(" maxUs=")
            .Append(metric.MaxMicroseconds.ToString("0.###"))
            .Append(' ')
            .Append(label1)
            .Append('=')
            .Append(value1)
            .Append(' ')
            .Append(label2)
            .Append('=')
            .Append(value2);

        if (!string.IsNullOrEmpty(extraLabel)) {
            builder.Append(' ')
                .Append(extraLabel)
                .Append('=')
                .Append(extraValue.ToString("0.##"));
        }

        LogMessageWithoutStackTrace(builder.ToString());
    }

    private static void LogMessageWithoutStackTrace(string text) {
        var logLock = LogLockField.GetValue(null);
        lock (logLock!) {
            var messageQueue = (LogMessageQueue)MessageQueueField.GetValue(null)!;
            messageQueue.Enqueue(new LogMessage(text), out var repeatsCapped);
            if (!repeatsCapped) {
                PostMessageMethod.Invoke(null, []);
            }
        }
    }

    private static void Reset(int startTick) {
        _windowStartTick = startTick;
        _attackTargetsCacheGetPotentialTargetsFor = default;
        _pawnPathFollowerPatherTick = default;
        _pawnStanceTrackerTick = default;
        _projectileTickInterval = default;
        _thingTakeDamage = default;
        _explosionTick = default;
        _thingDeSpawn = default;
    }

    public struct ExplosionTickState(long startTimestamp, bool projectileInstigator, int pendingCellsBefore) {
        public readonly long StartTimestamp = startTimestamp;
        public readonly bool ProjectileInstigator = projectileInstigator;
        public readonly int PendingCellsBefore = pendingCellsBefore;
    }

    private struct Metric {
        private static readonly double TimestampToMilliseconds = 1000d / Stopwatch.Frequency;

        public long Calls;
        public long FlagACalls;
        public long FlagBCalls;
        private long ElapsedTicks;
        private long MaxElapsedTicks;
        private double ExtraMetricSum;
        private long ExtraMetricCalls;

        public double TotalMilliseconds => ElapsedTicks * TimestampToMilliseconds;
        public double AverageMicroseconds => Calls == 0 ? 0d : TotalMilliseconds * 1000d / Calls;
        public double MaxMicroseconds => MaxElapsedTicks * TimestampToMilliseconds * 1000d;
        public float AverageExtraMetric => ExtraMetricCalls == 0 ? 0f : (float)(ExtraMetricSum / ExtraMetricCalls);

        public void Record(long startTimestamp, bool flagA, bool flagB, float? extraMetric = null) {
            var elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
            Calls += 1L;
            ElapsedTicks += elapsedTicks;

            if (elapsedTicks > MaxElapsedTicks) {
                MaxElapsedTicks = elapsedTicks;
            }

            if (flagA) {
                FlagACalls += 1L;
            }

            if (flagB) {
                FlagBCalls += 1L;
            }

            if (!extraMetric.HasValue) return;

            ExtraMetricCalls += 1L;
            ExtraMetricSum += extraMetric.Value;
        }
    }
}

[HarmonyPatch(typeof(AttackTargetsCache), nameof(AttackTargetsCache.GetPotentialTargetsFor))]
public static class Debug_AttackTargetsCache_GetPotentialTargetsFor_Profiler {
    [UsedImplicitly]
    public static void Prefix(out long __state) {
        BattleHotspotSweepProfiler.TryStart(out __state);
    }

    [UsedImplicitly]
    public static void Postfix(long __state, IAttackTargetSearcher th, List<IAttackTarget> __result) {
        BattleHotspotSweepProfiler.RecordAttackTargetsCacheGetPotentialTargetsFor(__state, th, __result);
    }
}

[HarmonyPatch(typeof(Pawn_PathFollower), nameof(Pawn_PathFollower.PatherTick))]
public static class Debug_Pawn_PathFollower_PatherTick_Profiler {
    [UsedImplicitly]
    public static void Prefix(out long __state) {
        BattleHotspotSweepProfiler.TryStart(out __state);
    }

    [UsedImplicitly]
    public static void Postfix(long __state, Pawn_PathFollower __instance) {
        BattleHotspotSweepProfiler.RecordPawnPathFollowerPatherTick(__state, __instance);
    }
}

[HarmonyPatch(typeof(Pawn_StanceTracker), nameof(Pawn_StanceTracker.StanceTrackerTick))]
public static class Debug_Pawn_StanceTracker_StanceTrackerTick_Profiler {
    [UsedImplicitly]
    public static void Prefix(out long __state) {
        BattleHotspotSweepProfiler.TryStart(out __state);
    }

    [UsedImplicitly]
    public static void Postfix(long __state, Pawn_StanceTracker __instance) {
        BattleHotspotSweepProfiler.RecordPawnStanceTrackerTick(__state, __instance);
    }
}

[HarmonyPatch(typeof(Projectile), "TickInterval", [typeof(int)])]
public static class Debug_Projectile_TickInterval_Profiler {
    [UsedImplicitly]
    public static void Prefix(out long __state) {
        BattleHotspotSweepProfiler.TryStart(out __state);
    }

    [UsedImplicitly]
    public static void Postfix(long __state, Projectile __instance) {
        BattleHotspotSweepProfiler.RecordProjectileTickInterval(__state, __instance);
    }
}

[HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
public static class Debug_Thing_TakeDamage_Profiler {
    [UsedImplicitly]
    public static void Prefix(out long __state) {
        BattleHotspotSweepProfiler.TryStart(out __state);
    }

    [UsedImplicitly]
    public static void Postfix(long __state, Thing __instance, DamageInfo dinfo) {
        BattleHotspotSweepProfiler.RecordThingTakeDamage(__state, __instance, dinfo);
    }
}

[HarmonyPatch(typeof(Explosion), "Tick")]
public static class Debug_Explosion_Tick_Profiler {
    [UsedImplicitly]
    private static void Prefix(Explosion __instance, out BattleHotspotSweepProfiler.ExplosionTickState __state) {
        BattleHotspotSweepProfiler.TryStart(out var startTimestamp);
        __state = BattleHotspotSweepProfiler.CreateExplosionTickState(startTimestamp, __instance);
    }

    [UsedImplicitly]
    private static void Postfix(Explosion __instance, BattleHotspotSweepProfiler.ExplosionTickState __state) {
        BattleHotspotSweepProfiler.RecordExplosionTick(__state, __instance);
    }
}

[HarmonyPatch(typeof(Thing), nameof(Thing.DeSpawn))]
public static class Debug_Thing_DeSpawn_Profiler {
    [UsedImplicitly]
    public static void Prefix(out long __state) {
        BattleHotspotSweepProfiler.TryStart(out __state);
    }

    [UsedImplicitly]
    public static void Postfix(long __state, Thing __instance) {
        BattleHotspotSweepProfiler.RecordThingDeSpawn(__state, __instance);
    }
}
#endif