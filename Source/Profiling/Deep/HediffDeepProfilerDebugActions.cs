#if false
using JetBrains.Annotations;
using LudeonTK;

namespace Kingfisher.Profiling.Deep;

// Hotspot investigation toggle:
// Set to true together with the deep profiler patch file.
public static class HediffDeepProfilerDebugActions {
    [DebugAction("Kingfisher", "Enable Hediff profiler (Lookup+Immunity)", actionType = DebugActionType.Action,
        allowedGameStates = AllowedGameStates.IsCurrentlyOnMap)]
    [UsedImplicitly]
    public static void Enable() {
        HediffDeepProfiler.Enable();
    }
}
#endif
