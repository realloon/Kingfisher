using JetBrains.Annotations;
using LudeonTK;

namespace Kingfisher.Profiling.Aggregate;

public static class AggregateProfilerDebugActions {
    [UsedImplicitly]
    [DebugAction("Kingfisher", "Enable Aggregate profiler", actionType = DebugActionType.Action,
        allowedGameStates = AllowedGameStates.IsCurrentlyOnMap)]
    public static void Enable() {
        AggregateProfiler.Enable();
    }
}
