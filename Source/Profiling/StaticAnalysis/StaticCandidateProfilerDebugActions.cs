#if false
using JetBrains.Annotations;
using LudeonTK;

namespace Kingfisher.Profiling.StaticAnalysis;

public static class StaticCandidateProfilerDebugActions {
    [UsedImplicitly]
    [DebugAction("Kingfisher", "Enable static candidate profiler", actionType = DebugActionType.Action,
        allowedGameStates = AllowedGameStates.IsCurrentlyOnMap)]
    public static void Enable() {
        StaticCandidateProfiler.Enable();
    }
}
#endif