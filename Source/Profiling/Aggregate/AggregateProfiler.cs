#if DEBUG
using System.Diagnostics;
using System.Text;
using HarmonyLib;

namespace Kingfisher.Profiling.Aggregate;

public static class AggregateProfiler {
    private const int ReportWindowTicks = 600;

    private static long _gameTickElapsedTicks;
    private static long _gameTickCallCount;
    private static int _windowStartTick = -1;
    private static bool _patchInstalled;

    public static void Enable() {
        InstallPatch();
        ResetWindow(CurrentTick());
        Log.Message("[Kingfisher.AggregateProfiler] enabled.");
    }

    public static long BeginTick() => Stopwatch.GetTimestamp();

    public static void EndTick(long startTimestamp) {
        _gameTickCallCount++;
        _gameTickElapsedTicks += Stopwatch.GetTimestamp() - startTimestamp;
        NotifySingleTick();
    }

    private static void InstallPatch() {
        if (_patchInstalled) {
            return;
        }

        var harmony = new Harmony("Vortex.Kingfisher.AggregateProfiler");
        harmony.Patch(
            AccessTools.Method(typeof(TickManager), nameof(TickManager.DoSingleTick)),
            prefix: new HarmonyMethod(typeof(AggregateProfilerPatches), nameof(AggregateProfilerPatches.TickPrefix)),
            postfix: new HarmonyMethod(typeof(AggregateProfilerPatches), nameof(AggregateProfilerPatches.TickPostfix)));
        _patchInstalled = true;
    }

    private static void NotifySingleTick() {
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
        _gameTickElapsedTicks = 0L;
        _gameTickCallCount = 0L;
        _windowStartTick = startTick;
    }

    private static string BuildReport(int currentTick) {
        var builder = new StringBuilder(192);
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
        AppendEntry(builder, "GameTick", _gameTickElapsedTicks, _gameTickCallCount);

        return builder.ToString().TrimEnd();
    }

    private static int CurrentTick() => Find.TickManager?.TicksGame ?? -1;

    private static string FormatMilliseconds(long elapsedTicks) =>
        (elapsedTicks * 1000d / Stopwatch.Frequency).ToString("F3");

    private static string FormatMicroseconds(long elapsedTicks, long callCount) =>
        callCount == 0L
            ? "-"
            : (elapsedTicks * 1_000_000d / Stopwatch.Frequency / callCount).ToString("F3");

    private static void AppendEntry(StringBuilder builder, string name, long elapsedTicks, long callCount) {
        builder.Append(name);
        builder.Append(": ");
        builder.Append(FormatMilliseconds(elapsedTicks));
        builder.Append(" ms / ");
        builder.Append(callCount);
        builder.Append(" calls / ");
        builder.Append(FormatMicroseconds(elapsedTicks, callCount));
        builder.Append(" us avg");
    }
}
#endif