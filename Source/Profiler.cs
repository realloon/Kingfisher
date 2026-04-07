#if DEBUG
using System.Diagnostics;
using System.Text;
using HarmonyLib;

namespace Kingfisher;

public static class Profiler {
    private const int ReportIntervalTicks = 600;
    private const int MaxEntriesPerReport = 32;
    private const double MinimumTotalMillisecondsToReport = 0.05d;
    private const double MinimumPeakMillisecondsToReport = 0.05d;
    private static readonly Dictionary<string, Measurement> Measurements = [];
    private static readonly StringBuilder ReportBuilder = new();

    private static readonly LogMessageQueue MessageQueue =
        AccessTools.StaticFieldRefAccess<LogMessageQueue>(typeof(Log), "messageQueue");

    private static readonly object LogLock =
        AccessTools.StaticFieldRefAccess<object>(typeof(Log), "logLock");

    private static readonly Action PostMessage =
        AccessTools.MethodDelegate<Action>(AccessTools.Method(typeof(Log), "PostMessage"));

    [ThreadStatic]
    private static ScopeState? _currentScope;

    private static int _nextReportTick = ReportIntervalTicks;

    public static Scope Measure(string name) {
        var scopeState = new ScopeState(name, Stopwatch.GetTimestamp(), _currentScope);
        _currentScope = scopeState;
        return new Scope(scopeState);
    }

    private static void RecordMeasurement(string name, long selfTicks) {
        if (!Measurements.TryGetValue(name, out var measurement)) {
            measurement = new Measurement();
            Measurements.Add(name, measurement);
        }

        measurement.SelfTicks += selfTicks;
        measurement.CallCount += 1;
        measurement.MaxSelfTicks = Math.Max(measurement.MaxSelfTicks, selfTicks);

        TryReport();
    }

    private static void TryReport() {
        if (!Prefs.DevMode) return;

        var tickManager = Find.TickManager;
        if (tickManager == null) return;

        var currentTick = tickManager.TicksGame;
        if (currentTick < _nextReportTick) return;

        _nextReportTick = currentTick + ReportIntervalTicks;
        if (Measurements.Count == 0) return;

        var sortedMeasurements = Measurements
            .OrderByDescending(pair => pair.Value.MaxSelfTicks)
            .ThenByDescending(pair => pair.Value.SelfTicks / (double)pair.Value.CallCount)
            .ThenByDescending(pair => pair.Value.SelfTicks)
            .ToList();
        var eligibleMeasurements = sortedMeasurements
            .Where(pair => {
                var totalMilliseconds = TicksToMilliseconds(pair.Value.SelfTicks);
                var peakMilliseconds = TicksToMilliseconds(pair.Value.MaxSelfTicks);
                return totalMilliseconds >= MinimumTotalMillisecondsToReport ||
                       peakMilliseconds >= MinimumPeakMillisecondsToReport;
            })
            .ToList();
        var reportedEntryCount = 0;

        ReportBuilder.Clear();
        ReportBuilder.Append("[Kingfisher Profiler] ");
        ReportBuilder.Append("tick=");
        ReportBuilder.Append(currentTick);
        ReportBuilder.Append(" window=");
        ReportBuilder.Append(ReportIntervalTicks);
        ReportBuilder.AppendLine();

        foreach (var (name, measurement) in eligibleMeasurements) {
            var selfMilliseconds = TicksToMilliseconds(measurement.SelfTicks);
            var maxSelfMilliseconds = TicksToMilliseconds(measurement.MaxSelfTicks);
            var averageSelfMilliseconds = selfMilliseconds / measurement.CallCount;

            ReportBuilder.Append(" - ");
            ReportBuilder.Append(name);
            ReportBuilder.Append(", peakSelf=");
            ReportBuilder.Append(maxSelfMilliseconds.ToString("F4"));
            ReportBuilder.Append(" ms");
            ReportBuilder.Append(", avgSelf=");
            ReportBuilder.Append(averageSelfMilliseconds.ToString("F4"));
            ReportBuilder.Append(" ms");
            ReportBuilder.Append(", sumSelf=");
            ReportBuilder.Append(selfMilliseconds.ToString("F3"));
            ReportBuilder.Append(" ms");
            ReportBuilder.Append(", calls=");
            ReportBuilder.Append(measurement.CallCount);
            ReportBuilder.AppendLine();

            reportedEntryCount += 1;

            if (reportedEntryCount >= MaxEntriesPerReport) break;
        }

        var hiddenEntryCount = eligibleMeasurements.Count - reportedEntryCount;
        if (hiddenEntryCount > 0) {
            ReportBuilder.Append(" - ");
            ReportBuilder.Append(hiddenEntryCount);
            ReportBuilder.AppendLine(" more");
        }

        EmitLogMessage(ReportBuilder.ToString().TrimEnd());
        Measurements.Clear();
    }

    private static void EmitLogMessage(string text) {
        lock (LogLock) {
            MessageQueue.Enqueue(new LogMessage(LogMessageType.Message, text, string.Empty));
            PostMessage();
        }
    }

    private static double TicksToMilliseconds(long ticks) {
        return ticks * 1000d / Stopwatch.Frequency;
    }

    private sealed class Measurement {
        public int CallCount;
        public long MaxSelfTicks;
        public long SelfTicks;
    }

    internal sealed class ScopeState(string name, long startTimestamp, ScopeState? parent) {
        public long ChildTicks;
        public string Name { get; } = name;
        public ScopeState? Parent { get; } = parent;
        public long StartTimestamp { get; } = startTimestamp;
    }

    public readonly struct Scope : IDisposable {
        private readonly ScopeState? _state;

        internal Scope(ScopeState state) {
            _state = state;
        }

        public void Dispose() {
            if (_state == null) return;

            var elapsedTicks = Stopwatch.GetTimestamp() - _state.StartTimestamp;
            var selfTicks = Math.Max(0L, elapsedTicks - _state.ChildTicks);
            _currentScope = _state.Parent;
            _state.Parent?.ChildTicks += elapsedTicks;

            RecordMeasurement(_state.Name, selfTicks);
        }
    }
}
#endif