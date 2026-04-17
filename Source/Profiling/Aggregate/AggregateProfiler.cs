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

    public static ScopeState BeginScope(Probe probe) =>
        Enabled ? new ScopeState(probe, Stopwatch.GetTimestamp()) : default;

    public static void EndScope(ScopeState state) {
        if (!state.Active) {
            return;
        }

        var index = (int)state.Probe;
        ProbeCallCounts[index]++;
        ProbeElapsedTicks[index] += Stopwatch.GetTimestamp() - state.StartTimestamp;
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
        var builder = new StringBuilder(512);
        var windowTicks = Math.Max(0, currentTick - _windowStartTick);
        var gameTickElapsedTicks = ProbeElapsedTicks[(int)Probe.GameTick];

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
        builder.AppendLine("Ledger");

        var entries = new List<ProbeEntry>(ProbeCount);
        for (var i = 0; i < ProbeCount; i++) {
            if (ProbeCallCounts[i] == 0) {
                continue;
            }

            entries.Add(new ProbeEntry((Probe)i, ProbeElapsedTicks[i], ProbeCallCounts[i], gameTickElapsedTicks));
        }

        entries.Sort(static (left, right) => right.ElapsedTicks.CompareTo(left.ElapsedTicks));
        for (var i = 0; i < entries.Count; i++) {
            builder.Append(i + 1);
            builder.Append(". ");
            entries[i].AppendTo(builder);
            builder.AppendLine();
        }

        if (gameTickElapsedTicks > 0) {
            var accountedTicks = 0L;
            for (var i = 1; i < ProbeCount; i++) {
                accountedTicks += ProbeElapsedTicks[i];
            }

            var unattributedTicks = Math.Max(0L, gameTickElapsedTicks - accountedTicks);
            builder.Append(entries.Count + 1);
            builder.Append(". ");
            AppendEntry(
                builder,
                "Unattributed",
                unattributedTicks,
                0L,
                gameTickElapsedTicks);
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

    private static string FormatMicroseconds(long elapsedTicks, long callCount) =>
        callCount == 0L
            ? "-"
            : (elapsedTicks * 1_000_000d / Stopwatch.Frequency / callCount).ToString("F3");

    private static string FormatPercent(long elapsedTicks, long totalElapsedTicks) =>
        totalElapsedTicks <= 0L
            ? "-"
            : (elapsedTicks * 100d / totalElapsedTicks).ToString("F1");

    private static void AppendEntry(
        StringBuilder builder,
        string name,
        long elapsedTicks,
        long callCount,
        long totalGameTickElapsedTicks) {
        builder.Append(name);
        builder.Append(": ");
        builder.Append(FormatMilliseconds(elapsedTicks));
        builder.Append(" ms");

        if (callCount > 0L) {
            builder.Append(" / ");
            builder.Append(callCount);
            builder.Append(" calls / ");
            builder.Append(FormatMicroseconds(elapsedTicks, callCount));
            builder.Append(" us avg");
        }

        if (totalGameTickElapsedTicks > 0L && name != nameof(Probe.GameTick)) {
            builder.Append(" / ");
            builder.Append(FormatPercent(elapsedTicks, totalGameTickElapsedTicks));
            builder.Append("% tick");
        }
    }

    internal enum Probe {
        GameTick = 0,
        MapPreTick,
        TickList,
        WorldTick,
        MapPostTick
    }

    private const int ProbeCount = (int)Probe.MapPostTick + 1;

    public readonly struct ScopeState {
        public ScopeState(Probe probe, long startTimestamp) {
            Probe = probe;
            StartTimestamp = startTimestamp;
        }

        public Probe Probe { get; }

        public long StartTimestamp { get; }

        public bool Active => StartTimestamp != 0L;
    }

    private sealed class ProbeEntry(Probe probe, long elapsedTicks, long callCount, long totalGameTickElapsedTicks) {
        public long ElapsedTicks => elapsedTicks;

        public void AppendTo(StringBuilder builder) {
            AppendEntry(builder, probe.ToString(), elapsedTicks, callCount, totalGameTickElapsedTicks);
        }
    }
}
