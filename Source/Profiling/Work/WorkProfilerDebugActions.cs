#if DEBUG
using JetBrains.Annotations;
using LudeonTK;

namespace Kingfisher.Profiling.Work;

public static class WorkProfilerDebugActions {
    [UsedImplicitly]
    [DebugAction("Kingfisher", "Enable Work profiler", actionType = DebugActionType.Action,
        allowedGameStates = AllowedGameStates.IsCurrentlyOnMap)]
    public static void Enable() {
        WorkProfiler.Enable();
    }
}
#endif
