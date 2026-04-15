#if true
using System.Diagnostics;
using System.Text;
using HarmonyLib;

namespace Kingfisher.Profiling.Deep;

internal static class HediffDeepProfiler {
    private const int ReportWindowTicks = 600;
    private const int TopEntryCount = 8;
    private static readonly AccessTools.FieldRef<ImmunityHandler, List<ImmunityRecord>> ImmunityListRef =
        AccessTools.FieldRefAccess<ImmunityHandler, List<ImmunityRecord>>("immunityList");

    private static readonly long[] BucketElapsedTicks = new long[BucketCount];
    private static readonly long[] BucketCallCounts = new long[BucketCount];

    private static readonly Dictionary<Type, LookupStat> HediffCompLookupStats = [];
    private static readonly Dictionary<HediffDef, ImmunityDefStat> ImmunityDefStats = [];

    private static long _neededImmunitiesTotalInfoCount;
    private static long _neededImmunitiesTotalHediffCount;
    private static long _possibleToDevelopImmunityNaturallyCallCount;
    private static long _possibleToDevelopImmunityNaturallyCacheHitCount;
    private static long _possibleToDevelopImmunityNaturallyColdPathCount;
    private static long _possibleToDevelopImmunityNaturallyTrueCount;
    private static long _possibleToDevelopImmunityNaturallyFalseCount;
    private static int _windowStartTick = -1;

    private static bool Enabled { get; set; }

    public static void Enable() {
        Enabled = true;
        ResetWindow(CurrentTick());
        Log.Message("[Kingfisher.HediffProfiler.Deep] enabled.");
    }

    public static void Dump(bool resetAfterDump = true) {
        if (!Enabled) {
            Log.Message("[Kingfisher.HediffProfiler.Deep] profiler is disabled.");
            return;
        }

        Log.Message(BuildReport(CurrentTick()));
        if (resetAfterDump) {
            ResetWindow(CurrentTick());
        }
    }

    public static void NotifySingleTick() {
        if (!Enabled) {
            return;
        }

        var currentTick = CurrentTick();
        if (_windowStartTick < 0) {
            _windowStartTick = currentTick;
            return;
        }

        if (currentTick - _windowStartTick >= ReportWindowTicks) {
            Log.Message(BuildReport(currentTick));
            ResetWindow(currentTick);
        }
    }

    public static long BeginScope() => Enabled ? Stopwatch.GetTimestamp() : 0L;

    public static void EndNeededImmunitiesNow(long startTimestamp, ImmunityHandler handler,
        List<ImmunityHandler.ImmunityInfo>? result) {
        if (!TryFinish(Bucket.NeededImmunitiesNow, startTimestamp, out var elapsed)) {
            return;
        }

        var infoCount = result?.Count ?? 0;
        _neededImmunitiesTotalInfoCount += infoCount;
        _neededImmunitiesTotalHediffCount += handler?.pawn?.health?.hediffSet?.hediffs?.Count ?? 0;
    }

    public static int GetImmunityRecordCount(ImmunityHandler handler) {
        return handler == null ? 0 : ImmunityListRef(handler)?.Count ?? 0;
    }

    public static void EndTryAddImmunityRecord(long startTimestamp, int immunityCountBefore, ImmunityHandler handler,
        HediffDef def) {
        if (!TryFinish(Bucket.TryAddImmunityRecord, startTimestamp, out var elapsed) || def == null) {
            return;
        }

        var stat = GetOrCreateImmunityDefStat(def);
        stat.TryAddElapsedTicks += elapsed;
        stat.TryAddCalls++;
        if (GetImmunityRecordCount(handler) > immunityCountBefore) {
            stat.TryAddAddedCount++;
        }
    }

    public static void EndImmunityRecordExists(long startTimestamp, HediffDef def, bool exists) {
        if (!TryFinish(Bucket.ImmunityRecordExists, startTimestamp, out var elapsed) || def == null) {
            return;
        }

        var stat = GetOrCreateImmunityDefStat(def);
        stat.ExistsElapsedTicks += elapsed;
        stat.ExistsCalls++;
        if (exists) {
            stat.ExistsTrueCount++;
        }
        else {
            stat.ExistsFalseCount++;
        }
    }

    public static void EndTryGetComp(long startTimestamp, Type? requestedType, bool hit) {
        if (!TryFinish(Bucket.TryGetComp, startTimestamp, out var elapsed) || requestedType == null) {
            return;
        }

        var stat = GetOrCreateLookupStat(HediffCompLookupStats, requestedType);
        stat.ElapsedTicks += elapsed;
        stat.CallCount++;
        if (hit) {
            stat.HitCount++;
        }
        else {
            stat.MissCount++;
        }
    }

    public static long BeginPossibleToDevelopImmunityNaturallyColdPath() {
        if (!Enabled) {
            return 0L;
        }

        _possibleToDevelopImmunityNaturallyCallCount++;
        _possibleToDevelopImmunityNaturallyColdPathCount++;
        return Stopwatch.GetTimestamp();
    }

    public static void NotifyPossibleToDevelopImmunityNaturallyCacheHit(bool result) {
        if (!Enabled) {
            return;
        }

        _possibleToDevelopImmunityNaturallyCallCount++;
        _possibleToDevelopImmunityNaturallyCacheHitCount++;
        if (result) {
            _possibleToDevelopImmunityNaturallyTrueCount++;
        }
        else {
            _possibleToDevelopImmunityNaturallyFalseCount++;
        }
    }

    public static void EndPossibleToDevelopImmunityNaturallyColdPath(long startTimestamp, bool result) {
        if (!TryFinish(Bucket.PossibleToDevelopImmunityNaturallyColdPath, startTimestamp, out _)) {
            return;
        }

        if (result) {
            _possibleToDevelopImmunityNaturallyTrueCount++;
        }
        else {
            _possibleToDevelopImmunityNaturallyFalseCount++;
        }
    }

    public static void EndImmunityChangePerTick(long startTimestamp, HediffDef? def) {
        if (!TryFinish(Bucket.ImmunityChangePerTick, startTimestamp, out var elapsed) || def == null) {
            return;
        }

        var stat = GetOrCreateImmunityDefStat(def);
        stat.ChangePerTickElapsedTicks += elapsed;
        stat.ChangePerTickCalls++;
    }

    public static bool HasComp(Hediff? hediff, Type compType) {
        if (hediff is not HediffWithComps { comps: { } comps }) {
            return false;
        }

        for (var i = 0; i < comps.Count; i++) {
            if (compType.IsInstanceOfType(comps[i])) {
                return true;
            }
        }

        return false;
    }

    private static bool TryFinish(Bucket bucket, long startTimestamp, out long elapsed) {
        elapsed = 0L;
        if (startTimestamp == 0L || !Enabled) {
            return false;
        }

        elapsed = Stopwatch.GetTimestamp() - startTimestamp;
        var index = (int)bucket;
        BucketCallCounts[index]++;
        BucketElapsedTicks[index] += elapsed;
        return true;
    }

    private static LookupStat GetOrCreateLookupStat(Dictionary<Type, LookupStat> stats, Type type) {
        if (!stats.TryGetValue(type, out var stat)) {
            stat = new LookupStat(type);
            stats.Add(type, stat);
        }

        return stat;
    }

    private static ImmunityDefStat GetOrCreateImmunityDefStat(HediffDef def) {
        if (!ImmunityDefStats.TryGetValue(def, out var stat)) {
            stat = new ImmunityDefStat(def);
            ImmunityDefStats.Add(def, stat);
        }

        return stat;
    }

    private static string BuildReport(int currentTick) {
        var builder = new StringBuilder(2048);
        var windowTicks = Math.Max(0, currentTick - _windowStartTick);

        builder.Append("[Kingfisher.HediffProfiler.Deep] window ");
        builder.Append(windowTicks);
        builder.Append(" ticks");
        if (_windowStartTick >= 0) {
            builder.Append(" (");
            builder.Append(_windowStartTick);
            builder.Append(" -> ");
            builder.Append(currentTick);
            builder.Append(')');
        }

        builder.AppendLine();
        builder.AppendLine("Methods");
        AppendTopBuckets(builder);
        AppendNeededImmunitiesSummary(builder);
        AppendPossibleToDevelopImmunityNaturallySummary(builder);
        AppendLookupStats(builder, "Top HediffComp lookups", HediffCompLookupStats);
        AppendImmunityStats(builder);

        return builder.ToString().TrimEnd();
    }

    private static void AppendTopBuckets(StringBuilder builder) {
        var entries = new List<BucketEntry>(BucketCount);
        for (var i = 0; i < BucketCount; i++) {
            if (BucketCallCounts[i] == 0) {
                continue;
            }

            entries.Add(new BucketEntry((Bucket)i, BucketElapsedTicks[i], BucketCallCounts[i]));
        }

        entries.Sort(static (left, right) => right.ElapsedTicks.CompareTo(left.ElapsedTicks));
        AppendTop(builder, entries);
    }

    private static void AppendNeededImmunitiesSummary(StringBuilder builder) {
        var callCount = BucketCallCounts[(int)Bucket.NeededImmunitiesNow];
        if (callCount == 0) {
            return;
        }

        builder.AppendLine();
        builder.Append("NeededImmunitiesNow avg hediffs: ");
        builder.Append((_neededImmunitiesTotalHediffCount / (double)callCount).ToString("F2"));
        builder.Append(" / avg infos: ");
        builder.Append((_neededImmunitiesTotalInfoCount / (double)callCount).ToString("F2"));
        builder.AppendLine();
    }

    private static void AppendPossibleToDevelopImmunityNaturallySummary(StringBuilder builder) {
        if (_possibleToDevelopImmunityNaturallyCallCount == 0) {
            return;
        }

        builder.AppendLine();
        builder.Append("PossibleToDevelopImmunityNaturally calls: ");
        builder.Append(_possibleToDevelopImmunityNaturallyCallCount);
        builder.Append(" / cache-hit ");
        builder.Append(_possibleToDevelopImmunityNaturallyCacheHitCount);
        builder.Append(" / cold-path ");
        builder.Append(_possibleToDevelopImmunityNaturallyColdPathCount);
        builder.Append(" / true ");
        builder.Append(_possibleToDevelopImmunityNaturallyTrueCount);
        builder.Append(" / false ");
        builder.Append(_possibleToDevelopImmunityNaturallyFalseCount);
        builder.AppendLine();
    }

    private static void AppendLookupStats(StringBuilder builder, string title, Dictionary<Type, LookupStat> stats) {
        if (stats.Count == 0) {
            return;
        }

        builder.AppendLine();
        builder.AppendLine(title);

        var entries = new List<LookupStat>(stats.Count);
        foreach (var stat in stats.Values) {
            if (stat.CallCount != 0) {
                entries.Add(stat);
            }
        }

        entries.Sort(static (left, right) => right.ElapsedTicks.CompareTo(left.ElapsedTicks));
        AppendTop(builder, entries);
    }

    private static void AppendImmunityStats(StringBuilder builder) {
        if (ImmunityDefStats.Count == 0) {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("Top Immunity Defs");

        var entries = new List<ImmunityDefStat>(ImmunityDefStats.Count);
        foreach (var stat in ImmunityDefStats.Values) {
            if (stat.TryAddCalls != 0 || stat.ExistsCalls != 0) {
                entries.Add(stat);
            }
        }

        entries.Sort(static (left, right) => right.TotalElapsedTicks.CompareTo(left.TotalElapsedTicks));
        AppendTop(builder, entries);
    }

    private static void AppendTop<T>(StringBuilder builder, List<T> entries) where T : IReportEntry {
        if (entries.Count == 0) {
            builder.AppendLine("  (no data)");
            return;
        }

        var count = Math.Min(TopEntryCount, entries.Count);
        for (var i = 0; i < count; i++) {
            builder.Append(i + 1);
            builder.Append(". ");
            entries[i].AppendTo(builder);
            builder.AppendLine();
        }
    }

    private static void ResetWindow(int startTick) {
        Array.Clear(BucketElapsedTicks, 0, BucketElapsedTicks.Length);
        Array.Clear(BucketCallCounts, 0, BucketCallCounts.Length);
        HediffCompLookupStats.Clear();
        ImmunityDefStats.Clear();
        _neededImmunitiesTotalInfoCount = 0L;
        _neededImmunitiesTotalHediffCount = 0L;
        _possibleToDevelopImmunityNaturallyCallCount = 0L;
        _possibleToDevelopImmunityNaturallyCacheHitCount = 0L;
        _possibleToDevelopImmunityNaturallyColdPathCount = 0L;
        _possibleToDevelopImmunityNaturallyTrueCount = 0L;
        _possibleToDevelopImmunityNaturallyFalseCount = 0L;
        _windowStartTick = startTick;
    }

    private static int CurrentTick() => Find.TickManager?.TicksGame ?? -1;

    private static string FormatMilliseconds(long elapsedTicks) =>
        (elapsedTicks * 1000d / Stopwatch.Frequency).ToString("F3");

    private enum Bucket {
        NeededImmunitiesNow = 0,
        TryAddImmunityRecord,
        ImmunityRecordExists,
        TryGetComp,
        PossibleToDevelopImmunityNaturallyColdPath,
        ImmunityChangePerTick
    }

    private const int BucketCount = (int)Bucket.ImmunityChangePerTick + 1;

    private interface IReportEntry {
        void AppendTo(StringBuilder builder);
    }

    private sealed class BucketEntry(Bucket bucket, long elapsedTicks, long callCount) : IReportEntry {
        public long ElapsedTicks => elapsedTicks;

        public void AppendTo(StringBuilder builder) {
            builder.Append(bucket);
            builder.Append(": ");
            builder.Append(FormatMilliseconds(elapsedTicks));
            builder.Append(" ms / ");
            builder.Append(callCount);
            builder.Append(" calls");
        }
    }

    private sealed class LookupStat(Type requestedType) : IReportEntry {
        private Type RequestedType { get; } = requestedType;
        public long ElapsedTicks;
        public long CallCount;
        public long HitCount;
        public long MissCount;

        public void AppendTo(StringBuilder builder) {
            builder.Append(RequestedType.Name);
            builder.Append(": ");
            builder.Append(FormatMilliseconds(ElapsedTicks));
            builder.Append(" ms / ");
            builder.Append(CallCount);
            builder.Append(" calls / hit ");
            builder.Append(HitCount);
            builder.Append(" miss ");
            builder.Append(MissCount);
        }
    }

    private sealed class ImmunityDefStat(HediffDef def) : IReportEntry {
        private HediffDef Def { get; } = def;
        public long TryAddElapsedTicks;
        public long TryAddCalls;
        public long TryAddAddedCount;
        public long ExistsElapsedTicks;
        public long ExistsCalls;
        public long ExistsTrueCount;
        public long ExistsFalseCount;
        public long ChangePerTickElapsedTicks;
        public long ChangePerTickCalls;

        public long TotalElapsedTicks => TryAddElapsedTicks + ExistsElapsedTicks + ChangePerTickElapsedTicks;

        public void AppendTo(StringBuilder builder) {
            builder.Append(Def.defName);
            builder.Append(": tryAdd ");
            builder.Append(FormatMilliseconds(TryAddElapsedTicks));
            builder.Append(" ms / ");
            builder.Append(TryAddCalls);
            builder.Append(" calls / +");
            builder.Append(TryAddAddedCount);
            builder.Append(" | exists ");
            builder.Append(FormatMilliseconds(ExistsElapsedTicks));
            builder.Append(" ms / ");
            builder.Append(ExistsCalls);
            builder.Append(" calls / true ");
            builder.Append(ExistsTrueCount);
            builder.Append(" false ");
            builder.Append(ExistsFalseCount);
            builder.Append(" | changePerTick ");
            builder.Append(FormatMilliseconds(ChangePerTickElapsedTicks));
            builder.Append(" ms / ");
            builder.Append(ChangePerTickCalls);
            builder.Append(" calls");
        }
    }
}
#endif
