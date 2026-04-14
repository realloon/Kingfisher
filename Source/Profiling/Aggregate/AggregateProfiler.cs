using System.Diagnostics;
using System.Text;

namespace Kingfisher.Profiling.Aggregate;

internal static class AggregateProfiler {
    private const int ReportWindowTicks = 600;

    private static readonly long[] ProbeElapsedTicks = new long[ProbeCount];
    private static readonly long[] ProbeCallCounts = new long[ProbeCount];

    private static int _windowStartTick = -1;

    private static bool Enabled { get; set; }

    public static void Enable() {
        Enabled = true;
        ResetWindow(CurrentTick());
        Log.Message("[Kingfisher.AggregateProfiler] enabled.");
    }

    public static long BeginScope() => Enabled ? Stopwatch.GetTimestamp() : 0L;

    public static void EndScope(Probe probe, long startTimestamp) {
        if (startTimestamp == 0L) {
            return;
        }

        var index = (int)probe;
        ProbeCallCounts[index]++;
        ProbeElapsedTicks[index] += Stopwatch.GetTimestamp() - startTimestamp;
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
        _windowStartTick = startTick;
    }

    private static string BuildReport(int currentTick) {
        var builder = new StringBuilder(256);
        var windowTicks = Math.Max(0, currentTick - _windowStartTick);

        builder.Append("[Kingfisher.AggregateProfiler] window ");
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
        builder.AppendLine("Probes");

        var entries = new List<ProbeEntry>(ProbeCount);
        for (var i = 0; i < ProbeCount; i++) {
            if (ProbeCallCounts[i] == 0) {
                continue;
            }

            entries.Add(new ProbeEntry((Probe)i, ProbeElapsedTicks[i], ProbeCallCounts[i]));
        }

        entries.Sort(static (left, right) => right.ElapsedTicks.CompareTo(left.ElapsedTicks));
        for (var i = 0; i < entries.Count; i++) {
            builder.Append(i + 1);
            builder.Append(". ");
            entries[i].AppendTo(builder);
            builder.AppendLine();
        }

        if (entries.Count == 0) {
            builder.AppendLine("  (no data)");
        }

        return builder.ToString().TrimEnd();
    }

    private static int CurrentTick() => Find.TickManager?.TicksGame ?? -1;

    private static string FormatMilliseconds(long elapsedTicks) =>
        (elapsedTicks * 1000d / Stopwatch.Frequency).ToString("F3");

    internal enum Probe {
        GameTick = 0,
        HealthTick,
        HealthTickInterval
    }

    private const int ProbeCount = (int)Probe.HealthTickInterval + 1;

    private sealed class ProbeEntry(Probe probe, long elapsedTicks, long callCount) {
        public long ElapsedTicks => elapsedTicks;

        public void AppendTo(StringBuilder builder) {
            builder.Append(probe);
            builder.Append(": ");
            builder.Append(FormatMilliseconds(elapsedTicks));
            builder.Append(" ms / ");
            builder.Append(callCount);
            builder.Append(" calls");
        }
    }
}
