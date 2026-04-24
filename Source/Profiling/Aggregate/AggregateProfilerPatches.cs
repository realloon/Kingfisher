#if DEBUG
using JetBrains.Annotations;

// ReSharper disable InconsistentNaming

namespace Kingfisher.Profiling.Aggregate;

public static class AggregateProfilerPatches {
    [UsedImplicitly]
    public static void TickPrefix(out long __state) {
        __state = AggregateProfiler.BeginTick();
    }

    [UsedImplicitly]
    public static void TickPostfix(long __state) {
        AggregateProfiler.EndTick(__state);
    }
}
#endif