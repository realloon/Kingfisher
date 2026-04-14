#if false
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace Kingfisher.Profiling.Deep;

internal static class HediffDeepProfiler {
    private const int ReportWindowTicks = 600;
    private const int TopEntryCount = 8;
    private static readonly Type[] CompPostTickArguments = [typeof(float).MakeByRefType()];
    private static readonly Type[] CompPostTickIntervalArguments = [typeof(float).MakeByRefType(), typeof(int)];

    private static readonly long[] BucketElapsedTicks = new long[BucketCount];
    private static readonly long[] BucketCallCounts = new long[BucketCount];
    private static readonly long[] InjuryBucketElapsedTicks = new long[InjuryBucketCount];
    private static readonly long[] InjuryBucketCallCounts = new long[InjuryBucketCount];

    private static readonly Dictionary<HediffDef, HediffStat> HediffStats = [];
    private static readonly Dictionary<Type, TypeStat> HediffCompStats = [];
    private static readonly Dictionary<PawnCapacityDef, CapacityStat> CapacityStats = [];
    private static readonly Dictionary<HediffDef, InjuryHediffStat> InjuryHediffStats = [];
    private static readonly Dictionary<InjuryCompKey, InjuryCompStat> InjuryCompStats = [];
    private static readonly Dictionary<InjuryCompKey, Type?> InjuryCompImplementationCache = [];

    [ThreadStatic]
    private static int _hediffTickDepth;

    [ThreadStatic]
    private static int _hediffPostTickDepth;

    [ThreadStatic]
    private static int _hediffTickIntervalDepth;

    [ThreadStatic]
    private static int _hediffPostTickIntervalDepth;

    [ThreadStatic]
    private static int _hediffCompPostTickDepth;

    [ThreadStatic]
    private static int _hediffCompPostTickIntervalDepth;

    public static bool Enabled { get; private set; }

    private static int _windowStartTick = -1;

    public static void Enable() {
        Enabled = true;
        ResetWindow(CurrentTick());
        Log.Message("[Kingfisher.HediffProfiler.Deep] enabled.");
    }

    public static void Disable() {
        if (!Enabled) {
            return;
        }

        Dump(resetAfterDump: false);
        Enabled = false;
        Log.Message("[Kingfisher.HediffProfiler.Deep] disabled.");
    }

    public static void Reset() {
        ResetWindow(CurrentTick());
        Log.Message("[Kingfisher.HediffProfiler.Deep] reset.");
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

    public static void EndScope(Bucket bucket, long startTimestamp) {
        if (startTimestamp == 0L) {
            return;
        }

        RecordBucket(bucket, Stopwatch.GetTimestamp() - startTimestamp);
    }

    public static long BeginHediffTick() => BeginDeepNestedScope(ref _hediffTickDepth);

    public static void EndHediffTick(long startTimestamp, Hediff hediff) {
        EndNestedHediffScope(ref _hediffTickDepth, Bucket.HediffTick, startTimestamp, hediff);
    }

    public static long BeginHediffPostTick() => BeginDeepNestedScope(ref _hediffPostTickDepth);

    public static void EndHediffPostTick(long startTimestamp, Hediff hediff) {
        EndNestedHediffScope(ref _hediffPostTickDepth, Bucket.HediffPostTick, startTimestamp, hediff);
    }

    public static long BeginHediffTickInterval() => BeginDeepNestedScope(ref _hediffTickIntervalDepth);

    public static void EndHediffTickInterval(long startTimestamp, Hediff hediff) {
        EndNestedHediffScope(ref _hediffTickIntervalDepth, Bucket.HediffTickInterval, startTimestamp, hediff);
    }

    public static long BeginHediffPostTickInterval() => BeginDeepNestedScope(ref _hediffPostTickIntervalDepth);

    public static void EndHediffPostTickInterval(long startTimestamp, Hediff hediff) {
        EndNestedHediffScope(ref _hediffPostTickIntervalDepth, Bucket.HediffPostTickInterval, startTimestamp, hediff);
    }

    public static long BeginHediffCompPostTick() => BeginDeepNestedScope(ref _hediffCompPostTickDepth);

    public static void EndHediffCompPostTick(long startTimestamp, HediffComp comp) {
        EndNestedCompScope(ref _hediffCompPostTickDepth, Bucket.HediffCompPostTick, startTimestamp, comp);
    }

    public static long BeginHediffCompPostTickInterval() => BeginDeepNestedScope(ref _hediffCompPostTickIntervalDepth);

    public static void EndHediffCompPostTickInterval(long startTimestamp, HediffComp comp) {
        EndNestedCompScope(ref _hediffCompPostTickIntervalDepth, Bucket.HediffCompPostTickInterval, startTimestamp,
            comp);
    }

    public static void RecordCapacityRecompute(PawnCapacityDef capacity, long startTimestamp) {
        if (startTimestamp == 0L) {
            return;
        }

        var elapsed = Stopwatch.GetTimestamp() - startTimestamp;
        RecordBucket(Bucket.CapacityRecompute, elapsed);

        if (!Enabled || capacity == null) {
            return;
        }

        if (!CapacityStats.TryGetValue(capacity, out var stat)) {
            stat = new CapacityStat(capacity);
            CapacityStats.Add(capacity, stat);
        }

        stat.ElapsedTicks += elapsed;
        stat.CallCount++;
    }

    public static void RecordAddHediff(Hediff hediff) {
        if (!Enabled || hediff?.def == null) {
            return;
        }

        RecordBucket(Bucket.AddHediff, 0L);
        var stat = GetOrCreateHediffStat(hediff.def);
        stat.AddCount++;
    }

    public static void RecordRemoveHediff(Hediff hediff) {
        if (!Enabled || hediff?.def == null) {
            return;
        }

        RecordBucket(Bucket.RemoveHediff, 0L);
        var stat = GetOrCreateHediffStat(hediff.def);
        stat.RemoveCount++;
    }

    public static IEnumerable<MethodBase> TargetMethods(Type baseType, string methodName, params Type[] argumentTypes) {
        var methods = new HashSet<MethodBase>();
        var baseMethod = baseType.GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, argumentTypes, null);
        if (baseMethod != null && !baseMethod.IsAbstract) {
            methods.Add(baseMethod);
        }

        foreach (var type in baseType.Assembly.GetTypes()) {
            if (type.IsAbstract || !baseType.IsAssignableFrom(type)) {
                continue;
            }

            var method = type.GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null,
                argumentTypes, null);
            if (method != null && !method.IsAbstract) {
                methods.Add(method);
            }
        }

        return methods;
    }

    public static bool IsDeepMode => Enabled;

    private static long BeginDeepNestedScope(ref int depth) {
        var enabled = IsDeepMode;
        depth++;
        if (!enabled || depth != 1) {
            return 0L;
        }

        return Stopwatch.GetTimestamp();
    }

    private static void EndNestedHediffScope(ref int depth, Bucket bucket, long startTimestamp, Hediff hediff) {
        if (depth > 0) {
            depth--;
        }

        if (startTimestamp == 0L || depth != 0 || !IsDeepMode || hediff == null) {
            return;
        }

        var elapsed = Stopwatch.GetTimestamp() - startTimestamp;
        RecordBucket(bucket, elapsed);

        if (hediff is Hediff_Injury) {
            switch (bucket) {
                case Bucket.HediffPostTick:
                    RecordInjuryHediff(hediff, InjuryBucket.PostTick, elapsed);
                    break;
                case Bucket.HediffTickInterval:
                    RecordInjuryHediff(hediff, InjuryBucket.TickInterval, elapsed);
                    break;
                case Bucket.HediffPostTickInterval:
                    RecordInjuryHediff(hediff, InjuryBucket.PostTickInterval, elapsed);
                    break;
            }
        }

        if (hediff.def == null) {
            return;
        }

        var stat = GetOrCreateHediffStat(hediff.def);
        stat.ElapsedTicks += elapsed;
        stat.CallCount++;
    }

    private static void EndNestedCompScope(ref int depth, Bucket bucket, long startTimestamp, HediffComp comp) {
        if (depth > 0) {
            depth--;
        }

        if (startTimestamp == 0L || depth != 0 || !IsDeepMode || comp == null) {
            return;
        }

        var elapsed = Stopwatch.GetTimestamp() - startTimestamp;
        RecordBucket(bucket, elapsed);

        if (comp.parent is Hediff_Injury) {
            if (bucket == Bucket.HediffCompPostTick) {
                RecordInjuryComp(comp, CompHookKind.PostTick, elapsed);
            } else if (bucket == Bucket.HediffCompPostTickInterval) {
                RecordInjuryComp(comp, CompHookKind.PostTickInterval, elapsed);
            }
        }

        var compType = comp.GetType();
        if (!HediffCompStats.TryGetValue(compType, out var stat)) {
            stat = new TypeStat(compType);
            HediffCompStats.Add(compType, stat);
        }

        stat.ElapsedTicks += elapsed;
        stat.CallCount++;
    }

    private static void RecordBucket(Bucket bucket, long elapsedTicks) {
        if (!Enabled) {
            return;
        }

        var index = (int)bucket;
        BucketCallCounts[index]++;
        BucketElapsedTicks[index] += elapsedTicks;
    }

    private static HediffStat GetOrCreateHediffStat(HediffDef def) {
        if (!HediffStats.TryGetValue(def, out var stat)) {
            stat = new HediffStat(def);
            HediffStats.Add(def, stat);
        }

        return stat;
    }

    private static string BuildReport(int currentTick) {
        var windowTicks = Math.Max(0, currentTick - _windowStartTick);
        var builder = new StringBuilder(2048);

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
        builder.AppendLine("Buckets");
        AppendTopBuckets(builder);
        AppendTopHediffs(builder);
        AppendTopHediffComps(builder);
        AppendTopCapacities(builder);
        AppendInjuryHotpath(builder);

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

    private static void AppendTopHediffs(StringBuilder builder) {
        if (HediffStats.Count == 0) {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("Top HediffDefs");

        var entries = new List<HediffStat>(HediffStats.Count);
        foreach (var stat in HediffStats.Values) {
            if (stat.ElapsedTicks == 0L && stat.AddCount == 0L && stat.RemoveCount == 0L) {
                continue;
            }

            entries.Add(stat);
        }

        entries.Sort(static (left, right) => right.ElapsedTicks.CompareTo(left.ElapsedTicks));
        AppendTop(builder, entries);
    }

    private static void AppendTopHediffComps(StringBuilder builder) {
        if (HediffCompStats.Count == 0) {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("Top HediffComps");

        var entries = new List<TypeStat>(HediffCompStats.Count);
        foreach (var stat in HediffCompStats.Values) {
            if (stat.ElapsedTicks == 0L) {
                continue;
            }

            entries.Add(stat);
        }

        entries.Sort(static (left, right) => right.ElapsedTicks.CompareTo(left.ElapsedTicks));
        AppendTop(builder, entries);
    }

    private static void AppendTopCapacities(StringBuilder builder) {
        if (CapacityStats.Count == 0) {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("Top Capacity Recomputes");

        var entries = new List<CapacityStat>(CapacityStats.Count);
        foreach (var stat in CapacityStats.Values) {
            if (stat.ElapsedTicks == 0L) {
                continue;
            }

            entries.Add(stat);
        }

        entries.Sort(static (left, right) => right.ElapsedTicks.CompareTo(left.ElapsedTicks));
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
        Array.Clear(InjuryBucketElapsedTicks, 0, InjuryBucketElapsedTicks.Length);
        Array.Clear(InjuryBucketCallCounts, 0, InjuryBucketCallCounts.Length);
        HediffStats.Clear();
        HediffCompStats.Clear();
        CapacityStats.Clear();
        InjuryHediffStats.Clear();
        InjuryCompStats.Clear();
        _windowStartTick = startTick;
    }

    private static int CurrentTick() => Find.TickManager?.TicksGame ?? -1;

    private static string FormatMilliseconds(long elapsedTicks) =>
        (elapsedTicks * 1000d / Stopwatch.Frequency).ToString("F3");

    internal enum Bucket {
        GameTick = 0,
        HealthTick,
        HealthTickInterval,
        HediffTick,
        HediffPostTick,
        HediffTickInterval,
        HediffPostTickInterval,
        HediffCompPostTick,
        HediffCompPostTickInterval,
        ImmunityTickInterval,
        DirtyCache,
        CapacityRecompute,
        AddHediff,
        RemoveHediff
    }

    private const int BucketCount = (int)Bucket.RemoveHediff + 1;

    private enum InjuryBucket {
        PostTick = 0,
        TickInterval,
        PostTickInterval,
        CompPostTick,
        CompPostTickInterval
    }

    private const int InjuryBucketCount = (int)InjuryBucket.CompPostTickInterval + 1;

    private enum CompHookKind {
        PostTick = 0,
        PostTickInterval
    }

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

    private sealed class HediffStat(HediffDef def) : IReportEntry {
        public HediffDef Def { get; } = def;

        public long ElapsedTicks;
        public long CallCount;
        public long AddCount;
        public long RemoveCount;

        public void AppendTo(StringBuilder builder) {
            builder.Append(Def?.defName ?? "<null>");
            builder.Append(": ");
            builder.Append(FormatMilliseconds(ElapsedTicks));
            builder.Append(" ms / ");
            builder.Append(CallCount);
            builder.Append(" calls");
            if (AddCount != 0 || RemoveCount != 0) {
                builder.Append(" / +");
                builder.Append(AddCount);
                builder.Append(" -");
                builder.Append(RemoveCount);
            }
        }
    }

    private sealed class TypeStat(Type type) : IReportEntry {
        public Type Type { get; } = type;

        public long ElapsedTicks;
        public long CallCount;

        public void AppendTo(StringBuilder builder) {
            builder.Append(Type.Name);
            builder.Append(": ");
            builder.Append(FormatMilliseconds(ElapsedTicks));
            builder.Append(" ms / ");
            builder.Append(CallCount);
            builder.Append(" calls");
        }
    }

    private sealed class CapacityStat(PawnCapacityDef capacity) : IReportEntry {
        public PawnCapacityDef Capacity { get; } = capacity;

        public long ElapsedTicks;
        public long CallCount;

        public void AppendTo(StringBuilder builder) {
            builder.Append(Capacity.defName);
            builder.Append(": ");
            builder.Append(FormatMilliseconds(ElapsedTicks));
            builder.Append(" ms / ");
            builder.Append(CallCount);
            builder.Append(" calls");
        }
    }

    private static void AppendInjuryHotpath(StringBuilder builder) {
        var hasInjuryData = false;
        for (var i = 0; i < InjuryBucketCount; i++) {
            if (InjuryBucketCallCounts[i] != 0) {
                hasInjuryData = true;
                break;
            }
        }

        if (!hasInjuryData) {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("Injury Hotpath");
        for (var i = 0; i < InjuryBucketCount; i++) {
            if (InjuryBucketCallCounts[i] == 0) {
                continue;
            }

            builder.Append((InjuryBucket)i);
            builder.Append(": ");
            builder.Append(FormatMilliseconds(InjuryBucketElapsedTicks[i]));
            builder.Append(" ms / ");
            builder.Append(InjuryBucketCallCounts[i]);
            builder.Append(" calls");
            builder.AppendLine();
        }

        AppendTopInjuryHediffs(builder);
        AppendTopInjuryCompHooks(builder, onlyBaseDispatch: false);
        AppendTopInjuryCompHooks(builder, onlyBaseDispatch: true);
    }

    private static void AppendTopInjuryHediffs(StringBuilder builder) {
        builder.AppendLine("Top Injury HediffDefs");

        var entries = new List<InjuryHediffStat>(InjuryHediffStats.Count);
        foreach (var stat in InjuryHediffStats.Values) {
            if (stat.TotalElapsedTicks == 0L) {
                continue;
            }

            entries.Add(stat);
        }

        entries.Sort(static (left, right) => right.TotalElapsedTicks.CompareTo(left.TotalElapsedTicks));
        AppendTop(builder, entries);
    }

    private static void AppendTopInjuryCompHooks(StringBuilder builder, bool onlyBaseDispatch) {
        builder.AppendLine(onlyBaseDispatch ? "Top Injury Empty Comp Hooks" : "Top Injury Comp Hooks");

        var entries = new List<InjuryCompStat>(InjuryCompStats.Count);
        foreach (var stat in InjuryCompStats.Values) {
            if (stat.ElapsedTicks == 0L) {
                continue;
            }

            if (onlyBaseDispatch && stat.ImplementationType != typeof(HediffComp)) {
                continue;
            }

            if (!onlyBaseDispatch && stat.ImplementationType == typeof(HediffComp)) {
                continue;
            }

            entries.Add(stat);
        }

        entries.Sort(static (left, right) => right.ElapsedTicks.CompareTo(left.ElapsedTicks));
        AppendTop(builder, entries);
    }

    private static void RecordInjuryHediff(Hediff hediff, InjuryBucket bucket, long elapsedTicks) {
        var bucketIndex = (int)bucket;
        InjuryBucketElapsedTicks[bucketIndex] += elapsedTicks;
        InjuryBucketCallCounts[bucketIndex]++;

        if (hediff.def == null) {
            return;
        }

        if (!InjuryHediffStats.TryGetValue(hediff.def, out var stat)) {
            stat = new InjuryHediffStat(hediff.def);
            InjuryHediffStats.Add(hediff.def, stat);
        }

        stat.Record(bucket, elapsedTicks);
    }

    private static void RecordInjuryComp(HediffComp comp, CompHookKind hook, long elapsedTicks) {
        var injuryBucket = hook == CompHookKind.PostTick
            ? InjuryBucket.CompPostTick
            : InjuryBucket.CompPostTickInterval;
        var bucketIndex = (int)injuryBucket;
        InjuryBucketElapsedTicks[bucketIndex] += elapsedTicks;
        InjuryBucketCallCounts[bucketIndex]++;

        var key = new InjuryCompKey(comp.GetType(), hook);
        if (!InjuryCompStats.TryGetValue(key, out var stat)) {
            var implementationType = GetCompImplementationType(key);
            stat = new InjuryCompStat(comp.GetType(), hook, implementationType);
            InjuryCompStats.Add(key, stat);
        }

        stat.ElapsedTicks += elapsedTicks;
        stat.CallCount++;
    }

    private static Type? GetCompImplementationType(InjuryCompKey key) {
        if (InjuryCompImplementationCache.TryGetValue(key, out var implementationType)) {
            return implementationType;
        }

        var methodName = key.Hook == CompHookKind.PostTick
            ? nameof(HediffComp.CompPostTick)
            : nameof(HediffComp.CompPostTickInterval);
        var arguments = key.Hook == CompHookKind.PostTick ? CompPostTickArguments : CompPostTickIntervalArguments;

        for (var type = key.CompType; type != null && typeof(HediffComp).IsAssignableFrom(type); type = type.BaseType) {
            var method = type.GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null,
                arguments, null);
            if (method != null) {
                implementationType = method.DeclaringType;
                InjuryCompImplementationCache[key] = implementationType;
                return implementationType;
            }
        }

        InjuryCompImplementationCache[key] = null;
        return null;
    }

    private sealed class InjuryHediffStat(HediffDef def) : IReportEntry {
        public HediffDef Def { get; } = def;

        public long PostTickElapsedTicks;
        public long PostTickCalls;
        public long TickIntervalElapsedTicks;
        public long TickIntervalCalls;
        public long PostTickIntervalElapsedTicks;
        public long PostTickIntervalCalls;

        public long TotalElapsedTicks => PostTickElapsedTicks + TickIntervalElapsedTicks + PostTickIntervalElapsedTicks;

        public void Record(InjuryBucket bucket, long elapsedTicks) {
            switch (bucket) {
                case InjuryBucket.PostTick:
                    PostTickElapsedTicks += elapsedTicks;
                    PostTickCalls++;
                    break;
                case InjuryBucket.TickInterval:
                    TickIntervalElapsedTicks += elapsedTicks;
                    TickIntervalCalls++;
                    break;
                case InjuryBucket.PostTickInterval:
                    PostTickIntervalElapsedTicks += elapsedTicks;
                    PostTickIntervalCalls++;
                    break;
            }
        }

        public void AppendTo(StringBuilder builder) {
            builder.Append(Def.defName);
            builder.Append(": total ");
            builder.Append(FormatMilliseconds(TotalElapsedTicks));
            builder.Append(" ms / post ");
            builder.Append(FormatMilliseconds(PostTickElapsedTicks));
            builder.Append(" ms / interval ");
            builder.Append(FormatMilliseconds(TickIntervalElapsedTicks));
            builder.Append(" ms / postInterval ");
            builder.Append(FormatMilliseconds(PostTickIntervalElapsedTicks));
            builder.Append(" ms");
        }
    }

    private readonly struct InjuryCompKey : IEquatable<InjuryCompKey> {
        public InjuryCompKey(Type compType, CompHookKind hook) {
            CompType = compType;
            Hook = hook;
        }

        public Type CompType { get; }
        public CompHookKind Hook { get; }

        public bool Equals(InjuryCompKey other) => CompType == other.CompType && Hook == other.Hook;

        public override bool Equals(object? obj) => obj is InjuryCompKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(CompType, (int)Hook);
    }

    private sealed class InjuryCompStat(Type compType, CompHookKind hook, Type? implementationType) : IReportEntry {
        public Type CompType { get; } = compType;
        public CompHookKind Hook { get; } = hook;
        public Type? ImplementationType { get; } = implementationType;

        public long ElapsedTicks;
        public long CallCount;

        public void AppendTo(StringBuilder builder) {
            builder.Append(CompType.Name);
            builder.Append('.');
            builder.Append(Hook == CompHookKind.PostTick
                ? nameof(HediffComp.CompPostTick)
                : nameof(HediffComp.CompPostTickInterval));
            builder.Append(": ");
            builder.Append(FormatMilliseconds(ElapsedTicks));
            builder.Append(" ms / ");
            builder.Append(CallCount);
            builder.Append(" calls");
            if (ImplementationType == typeof(HediffComp)) {
                builder.Append(" / base-empty");
            }
        }
    }
}
#endif