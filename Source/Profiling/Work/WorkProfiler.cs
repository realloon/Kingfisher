#if DEBUG
using System.Diagnostics;
using System.Text;
using Verse.AI;

namespace Kingfisher.Profiling.Work;

public static class WorkProfiler {
    private const int ReportWindowTicks = 600;

    private static readonly long[] ProbeElapsedTicks = new long[ProbeCount];
    private static readonly long[] ProbeCallCounts = new long[ProbeCount];
    private static readonly Dictionary<string, WorkGiverStats> WorkGiverStatsByName = [];
    private static readonly Dictionary<int, PawnStats> IdlePawnStatsById = [];

    [ThreadStatic]
    private static ScanFrame? _currentFrame;

    private static int _windowStartTick = -1;
    private static long _workScanElapsedTicks;
    private static long _workScanCallCounts;
    private static long _noJobElapsedTicks;
    private static long _noJobCallCounts;

    private static bool Enabled { get; set; }

    public static void Enable() {
        Enabled = true;
        ResetWindow(CurrentTick());
        Log.Message("[Kingfisher.WorkProfiler] enabled.");
    }

    public static WorkScanState BeginWorkScan(Pawn pawn) {
        if (!Enabled || !ShouldProfile(pawn)) {
            return default;
        }

        var previousFrame = _currentFrame;
        _currentFrame = new ScanFrame(pawn, previousFrame);
        return new WorkScanState(Stopwatch.GetTimestamp(), previousFrame);
    }

    public static void EndWorkScan(WorkScanState state, ThinkResult result) {
        if (!state.Active) {
            return;
        }

        var elapsedTicks = Stopwatch.GetTimestamp() - state.StartTimestamp;
        _workScanCallCounts++;
        _workScanElapsedTicks += elapsedTicks;

        if (!result.IsValid && _currentFrame != null) {
            _noJobCallCounts++;
            _noJobElapsedTicks += elapsedTicks;
            RecordIdlePawn(_currentFrame.Pawn, elapsedTicks);
        }

        _currentFrame = state.PreviousFrame;
    }

    public static ScopeState BeginProbe(Probe probe, WorkGiver? giver) {
        if (!Enabled || _currentFrame == null) {
            return default;
        }

        return new ScopeState(probe, Stopwatch.GetTimestamp(), GetWorkGiverName(giver));
    }

    public static void EndProbe(ScopeState state) {
        if (!state.Active) {
            return;
        }

        var elapsedTicks = Stopwatch.GetTimestamp() - state.StartTimestamp;
        var index = (int)state.Probe;
        ProbeCallCounts[index]++;
        ProbeElapsedTicks[index] += elapsedTicks;

        if (state.WorkGiverName != null) {
            WorkGiverStatsByName.TryGetValue(state.WorkGiverName, out var workGiverStats);
            WorkGiverStatsByName[state.WorkGiverName] = workGiverStats.Add(elapsedTicks);
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

        if (currentTick - _windowStartTick < ReportWindowTicks) {
            return;
        }

        Log.Message(BuildReport(currentTick));
        ResetWindow(currentTick);
    }

    private static void ResetWindow(int startTick) {
        Array.Clear(ProbeElapsedTicks, 0, ProbeElapsedTicks.Length);
        Array.Clear(ProbeCallCounts, 0, ProbeCallCounts.Length);
        WorkGiverStatsByName.Clear();
        IdlePawnStatsById.Clear();

        _workScanElapsedTicks = 0L;
        _workScanCallCounts = 0L;
        _noJobElapsedTicks = 0L;
        _noJobCallCounts = 0L;
        _windowStartTick = startTick;
    }

    private static string BuildReport(int currentTick) {
        var builder = new StringBuilder(1024);
        var windowTicks = Math.Max(0, currentTick - _windowStartTick);

        builder.Append("[Kingfisher.WorkProfiler] window ");
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
        builder.AppendLine("Scans");
        builder.Append("1. TryIssueJobPackage: ");
        AppendProbeStats(builder, _workScanElapsedTicks, _workScanCallCounts);
        builder.AppendLine();
        builder.Append("2. NoJob: ");
        AppendProbeStats(builder, _noJobElapsedTicks, _noJobCallCounts);
        builder.Append(" / rate ");
        builder.Append(FormatRatio(_noJobCallCounts, _workScanCallCounts));
        builder.AppendLine();

        builder.AppendLine();
        builder.AppendLine("Methods");

        var probeEntries = new List<ProbeEntry>(ProbeCount);
        for (var i = 0; i < ProbeCount; i++) {
            if (ProbeCallCounts[i] == 0L) {
                continue;
            }

            probeEntries.Add(new ProbeEntry((Probe)i, ProbeElapsedTicks[i], ProbeCallCounts[i]));
        }

        probeEntries.Sort(static (left, right) => right.ElapsedTicks.CompareTo(left.ElapsedTicks));
        for (var i = 0; i < probeEntries.Count; i++) {
            builder.Append(i + 1);
            builder.Append(". ");
            probeEntries[i].AppendTo(builder);
            builder.AppendLine();
        }

        if (probeEntries.Count == 0) {
            builder.AppendLine("  (no data)");
        }

        builder.AppendLine();
        builder.AppendLine("Top WorkGivers");
        AppendTopWorkGivers(builder);

        builder.AppendLine();
        builder.AppendLine("Top Idle Pawns");
        AppendTopIdlePawns(builder);

        return builder.ToString().TrimEnd();
    }

    private static void AppendTopWorkGivers(StringBuilder builder) {
        if (WorkGiverStatsByName.Count == 0) {
            builder.AppendLine("  (no data)");
            return;
        }

        var entries = WorkGiverStatsByName
            .OrderByDescending(static entry => entry.Value.ElapsedTicks)
            .Take(8)
            .ToArray();

        for (var i = 0; i < entries.Length; i++) {
            builder.Append(i + 1);
            builder.Append(". ");
            builder.Append(entries[i].Key);
            builder.Append(": ");
            AppendProbeStats(builder, entries[i].Value.ElapsedTicks, entries[i].Value.CallCount);
            builder.AppendLine();
        }
    }

    private static void AppendTopIdlePawns(StringBuilder builder) {
        if (IdlePawnStatsById.Count == 0) {
            builder.AppendLine("  (no data)");
            return;
        }

        var entries = IdlePawnStatsById
            .Values
            .OrderByDescending(static entry => entry.ElapsedTicks)
            .Take(8)
            .ToArray();

        for (var i = 0; i < entries.Length; i++) {
            builder.Append(i + 1);
            builder.Append(". ");
            builder.Append(entries[i].Name);
            builder.Append(": ");
            AppendProbeStats(builder, entries[i].ElapsedTicks, entries[i].CallCount);
            builder.AppendLine();
        }
    }

    private static void RecordIdlePawn(Pawn pawn, long elapsedTicks) {
        IdlePawnStatsById.TryGetValue(pawn.thingIDNumber, out var pawnStats);
        IdlePawnStatsById[pawn.thingIDNumber] = pawnStats.Add(pawn.LabelShortCap, elapsedTicks);
    }

    private static bool ShouldProfile(Pawn pawn) => pawn.IsColonistPlayerControlled;

    private static string? GetWorkGiverName(WorkGiver? giver) {
        if (giver == null) {
            return null;
        }

        return giver.def?.defName ?? giver.GetType().Name;
    }

    private static int CurrentTick() => Find.TickManager?.TicksGame ?? -1;

    private static void AppendProbeStats(StringBuilder builder, long elapsedTicks, long callCount) {
        builder.Append(FormatMilliseconds(elapsedTicks));
        builder.Append(" ms / ");
        builder.Append(callCount);
        builder.Append(" calls / ");
        builder.Append(FormatMicroseconds(elapsedTicks, callCount));
        builder.Append(" us avg");
    }

    private static string FormatMilliseconds(long elapsedTicks) =>
        (elapsedTicks * 1000d / Stopwatch.Frequency).ToString("F3");

    private static string FormatMicroseconds(long elapsedTicks, long callCount) =>
        callCount == 0L
            ? "-"
            : (elapsedTicks * 1_000_000d / Stopwatch.Frequency / callCount).ToString("F3");

    private static string FormatRatio(long numerator, long denominator) =>
        denominator == 0L ? "0.00%" : (numerator * 100d / denominator).ToString("F2") + "%";

    public enum Probe {
        PawnCanUseWorkGiver = 0,
        NonScanJob,
        PotentialWorkThingsGlobal,
        PotentialWorkCellsGlobal,
        HasJobOnThing,
        HasJobOnCell,
        JobOnThing,
        JobOnCell,
        GetPriority,
        ClosestThingGlobal,
        ClosestThingGlobalReachable,
        ClosestThingReachable,
        PawnCanReach
    }

    private const int ProbeCount = (int)Probe.PawnCanReach + 1;

    public readonly struct WorkScanState(long startTimestamp, ScanFrame? previousFrame) {
        public long StartTimestamp { get; } = startTimestamp;

        public ScanFrame? PreviousFrame { get; } = previousFrame;

        public bool Active => StartTimestamp != 0L;
    }

    public readonly struct ScopeState(Probe probe, long startTimestamp, string? workGiverName) {
        public Probe Probe { get; } = probe;

        public long StartTimestamp { get; } = startTimestamp;

        public string? WorkGiverName { get; } = workGiverName;

        public bool Active => StartTimestamp != 0L;
    }

    public sealed class ScanFrame(Pawn pawn, ScanFrame? previousFrame) {
        public Pawn Pawn { get; } = pawn;

        public ScanFrame? PreviousFrame { get; } = previousFrame;
    }

    private struct WorkGiverStats {
        public long ElapsedTicks;
        public long CallCount;

        public WorkGiverStats Add(long elapsedTicks) {
            ElapsedTicks += elapsedTicks;
            CallCount++;
            return this;
        }
    }

    private struct PawnStats {
        public string Name;
        public long ElapsedTicks;
        public long CallCount;

        public PawnStats Add(string name, long elapsedTicks) {
            Name = name;
            ElapsedTicks += elapsedTicks;
            CallCount++;
            return this;
        }
    }

    private sealed class ProbeEntry(Probe probe, long elapsedTicks, long callCount) {
        public long ElapsedTicks => elapsedTicks;

        public void AppendTo(StringBuilder builder) {
            builder.Append(probe);
            builder.Append(": ");
            AppendProbeStats(builder, elapsedTicks, callCount);
        }
    }
}
#endif
