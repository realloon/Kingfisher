#if false
using System.Diagnostics;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Verse.AI;

// ReSharper disable InconsistentNaming

namespace Kingfisher.Profiling.StaticAnalysis;

public static class StaticCandidateProfiler {
    private const int ReportWindowTicks = 600;
    private const string HarmonyId = "Vortex.Kingfisher.StaticCandidateProfiler";

    private static readonly MethodInfo BeginMethod = AccessTools.Method(
        typeof(StaticCandidateProfiler),
        nameof(Begin));

    private static readonly MethodInfo EndMethod = AccessTools.Method(
        typeof(StaticCandidateProfiler),
        nameof(End));

    private static readonly List<TargetMethod> Targets = [
        new("Pawn.TickInterval (inclusive)", () => AccessTools.Method(typeof(Pawn), "TickInterval",
            [typeof(int)])),
        new("Pawn_JobTracker.JobTrackerTickInterval", () => AccessTools.Method(typeof(Pawn_JobTracker),
            nameof(Pawn_JobTracker.JobTrackerTickInterval), [typeof(int)])),
        new("Pawn_JobTracker.DetermineNextJob", () => AccessTools.Method(typeof(Pawn_JobTracker),
            "DetermineNextJob", [typeof(ThinkTreeDef).MakeByRefType(), typeof(bool)])),
        new("JobGiver_Work.TryIssueJobPackage", () => AccessTools.Method(typeof(JobGiver_Work),
            nameof(JobGiver_Work.TryIssueJobPackage), [typeof(Pawn), typeof(JobIssueParams)]))
    ];

    private static readonly Dictionary<MethodBase, Entry> EntriesByMethod = [];

    private static int _windowStartTick = -1;
    private static bool _enabled;

    public static void Enable() {
        if (_enabled) {
            ResetWindow(CurrentTick());
            Log.Message("[Kingfisher.StaticCandidateProfiler] reset.");
            return;
        }

        InstallPatches();
        ResetWindow(CurrentTick());
        _enabled = true;
        Log.Message("[Kingfisher.StaticCandidateProfiler] enabled.");
    }

    public static void Begin(out long __state) {
        __state = Stopwatch.GetTimestamp();
    }

    public static Exception? End(Exception? __exception, long __state, MethodBase __originalMethod) {
        if (__state != 0L && _enabled && EntriesByMethod.TryGetValue(__originalMethod, out var entry)) {
            entry.Record(Stopwatch.GetTimestamp() - __state);
            MaybeReport();
        }

        return __exception;
    }

    private static void InstallPatches() {
        var harmony = new Harmony(HarmonyId);
        EntriesByMethod.Clear();

        foreach (var target in Targets) {
            var method = target.Resolve();
            if (method == null) {
                Log.Warning($"[Kingfisher.StaticCandidateProfiler] target not found: {target.Name}");
                continue;
            }

            EntriesByMethod.Add(method, new Entry(target.Name));
            harmony.Patch(
                method,
                prefix: new HarmonyMethod(BeginMethod),
                finalizer: new HarmonyMethod(EndMethod)
            );
        }
    }

    private static void MaybeReport() {
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
        foreach (var entry in EntriesByMethod.Values) {
            entry.Reset();
        }

        _windowStartTick = startTick;
    }

    private static string BuildReport(int currentTick) {
        var builder = new StringBuilder(2048);
        var windowTicks = Math.Max(0, currentTick - _windowStartTick);

        builder.Append("[Kingfisher.StaticCandidateProfiler] window ");
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
        builder.AppendLine("Inclusive timings; nested entries are not additive.");

        foreach (var entry in EntriesByMethod.Values
                     .Where(static entry => entry.CallCount > 0)
                     .OrderByDescending(static entry => entry.ElapsedTicks)) {
            AppendEntry(builder, entry);
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

    private static void AppendEntry(StringBuilder builder, Entry entry) {
        builder.Append(entry.Name);
        builder.Append(": ");
        builder.Append(FormatMilliseconds(entry.ElapsedTicks));
        builder.Append(" ms / ");
        builder.Append(entry.CallCount);
        builder.Append(" calls / ");
        builder.Append(FormatMicroseconds(entry.ElapsedTicks, entry.CallCount));
        builder.AppendLine(" us avg");
    }

    private sealed class TargetMethod(string name, Func<MethodBase?> resolve) {
        public readonly string Name = name;
        public readonly Func<MethodBase?> Resolve = resolve;
    }

    private sealed class Entry(string name) {
        public readonly string Name = name;
        public long ElapsedTicks;
        public long CallCount;

        public void Record(long elapsedTicks) {
            ElapsedTicks += elapsedTicks;
            CallCount++;
        }

        public void Reset() {
            ElapsedTicks = 0L;
            CallCount = 0L;
        }
    }
}
#endif
