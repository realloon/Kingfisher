#if DEBUG
using JetBrains.Annotations;
using HarmonyLib;
using RimWorld.Planet;

namespace Kingfisher.Profiling;

internal static class ProfilingScopeFactory {
    public static Profiler.Scope Start(string name) {
        return Profiler.Measure(name);
    }

    public static void Stop(Profiler.Scope scope) {
        scope.Dispose();
    }
}

[HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
[UsedImplicitly]
public static class TickManagerDoSingleTickProfilingPatch {
    [UsedImplicitly]
    public static void Prefix(out Profiler.Scope __state) {
        __state = ProfilingScopeFactory.Start("TickManager.DoSingleTick");
    }

    [UsedImplicitly]
    public static void Postfix(Profiler.Scope __state) {
        ProfilingScopeFactory.Stop(__state);
    }
}

[HarmonyPatch(typeof(TickList), nameof(TickList.Tick))]
[UsedImplicitly]
public static class TickListTickProfilingPatch {
    private static readonly AccessTools.FieldRef<TickList, TickerType> TickTypeRef =
        AccessTools.FieldRefAccess<TickList, TickerType>("tickType");

    [UsedImplicitly]
    public static void Prefix(TickList __instance, out Profiler.Scope __state) {
        __state = ProfilingScopeFactory.Start(GetScopeName(__instance));
    }

    [UsedImplicitly]
    public static void Postfix(Profiler.Scope __state) {
        ProfilingScopeFactory.Stop(__state);
    }

    private static string GetScopeName(TickList tickList) {
        return TickTypeRef(tickList) switch {
            TickerType.Normal => "TickList.Normal",
            TickerType.Rare => "TickList.Rare",
            TickerType.Long => "TickList.Long",
            _ => "TickList.Unknown"
        };
    }
}

[HarmonyPatch(typeof(Map), nameof(Map.MapPreTick))]
[UsedImplicitly]
public static class MapPreTickProfilingPatch {
    [UsedImplicitly]
    public static void Prefix(out Profiler.Scope __state) {
        __state = ProfilingScopeFactory.Start("Map.MapPreTick");
    }

    [UsedImplicitly]
    public static void Postfix(Profiler.Scope __state) {
        ProfilingScopeFactory.Stop(__state);
    }
}

[HarmonyPatch(typeof(Map), nameof(Map.MapPostTick))]
[UsedImplicitly]
public static class MapPostTickProfilingPatch {
    [UsedImplicitly]
    public static void Prefix(out Profiler.Scope __state) {
        __state = ProfilingScopeFactory.Start("Map.MapPostTick");
    }

    [UsedImplicitly]
    public static void Postfix(Profiler.Scope __state) {
        ProfilingScopeFactory.Stop(__state);
    }
}

[HarmonyPatch(typeof(World), nameof(World.WorldTick))]
[UsedImplicitly]
public static class WorldTickProfilingPatch {
    [UsedImplicitly]
    public static void Prefix(out Profiler.Scope __state) {
        __state = ProfilingScopeFactory.Start("World.WorldTick");
    }

    [UsedImplicitly]
    public static void Postfix(Profiler.Scope __state) {
        ProfilingScopeFactory.Stop(__state);
    }
}

[HarmonyPatch(typeof(World), nameof(World.WorldPostTick))]
[UsedImplicitly]
public static class WorldPostTickProfilingPatch {
    [UsedImplicitly]
    public static void Prefix(out Profiler.Scope __state) {
        __state = ProfilingScopeFactory.Start("World.WorldPostTick");
    }

    [UsedImplicitly]
    public static void Postfix(Profiler.Scope __state) {
        ProfilingScopeFactory.Stop(__state);
    }
}
#endif
