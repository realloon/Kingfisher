using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Kingfisher.Patches;

[HarmonyPatch(typeof(ListerBuildings), nameof(ListerBuildings.Add))]
public static class Postfix_ListerBuildings_Add {
    [UsedImplicitly]
    public static void Postfix(ListerBuildings __instance, Building b) {
        if (!ListerBuildingsColonistDefQueryCache.ShouldTrackColonistBuilding(b)) {
            return;
        }

        ListerBuildingsColonistDefQueryCache.NotifyAdded(__instance, b);
    }
}

[HarmonyPatch(typeof(ListerBuildings), nameof(ListerBuildings.Remove))]
public static class Prefix_ListerBuildings_Remove {
    [UsedImplicitly]
    public static void Prefix(ListerBuildings __instance, Building b) {
        if (b.def.building is { isNaturalRock: true }) {
            return;
        }

        ListerBuildingsColonistDefQueryCache.NotifyRemoved(__instance, b);
    }
}

[HarmonyPatch(typeof(ListerBuildings), nameof(ListerBuildings.AllBuildingsColonistOfDef))]
public static class Prefix_ListerBuildings_AllBuildingsColonistOfDef {
    [UsedImplicitly]
    public static bool Prefix(ListerBuildings __instance, ThingDef def, ref List<Building> __result) {
        __result = ListerBuildingsColonistDefQueryCache.CopyBuildingsOfDef(__instance, def);
        return false;
    }
}

[HarmonyPatch(typeof(ListerBuildings), nameof(ListerBuildings.ColonistsHaveBuilding), [typeof(ThingDef)])]
public static class Prefix_ListerBuildings_ColonistsHaveBuilding {
    [UsedImplicitly]
    public static bool Prefix(ListerBuildings __instance, ThingDef def, ref bool __result) {
        __result = ListerBuildingsColonistDefQueryCache.GetOrBuild(__instance, def).Count > 0;
        return false;
    }
}

[HarmonyPatch(typeof(ListerBuildings), nameof(ListerBuildings.ColonistsHaveBuildingWithPowerOn))]
public static class Prefix_ListerBuildings_ColonistsHaveBuildingWithPowerOn {
    [UsedImplicitly]
    public static bool Prefix(ListerBuildings __instance, ThingDef def, ref bool __result) {
        var buildings = ListerBuildingsColonistDefQueryCache.GetOrBuild(__instance, def);

        foreach (var t in buildings) {
            var compPowerTrader = t.TryGetComp<CompPowerTrader>();
            if (compPowerTrader is { PowerOn: false }) continue;
            __result = true;
            return false;
        }

        __result = false;
        return false;
    }
}

internal static class ListerBuildingsColonistDefQueryCache {
    private sealed class CacheState {
        public readonly Dictionary<ThingDef, List<Building>> ColonistBuildingsByDef = [];
    }

    private static readonly ConditionalWeakTable<ListerBuildings, CacheState> CacheTable = new();
    private static readonly List<Building> ResultBuffer = [];

    public static bool ShouldTrackColonistBuilding(Building building) =>
        building.Faction == Faction.OfPlayer && building.def.building is not { isNaturalRock: true };

    public static List<Building> CopyBuildingsOfDef(ListerBuildings listerBuildings, ThingDef def) {
        ResultBuffer.Clear();
        ResultBuffer.AddRange(GetOrBuild(listerBuildings, def));
        return ResultBuffer;
    }

    public static List<Building> GetOrBuild(ListerBuildings listerBuildings, ThingDef def) {
        var cache = CacheTable.GetValue(listerBuildings, static _ => new CacheState()).ColonistBuildingsByDef;
        if (cache.TryGetValue(def, out var buildings)) {
            return buildings;
        }

        buildings = [];
        var allBuildingsColonist = listerBuildings.allBuildingsColonist;
        foreach (var building in allBuildingsColonist) {
            if (building.def == def) {
                buildings.Add(building);
            }
        }

        cache.Add(def, buildings);
        return buildings;
    }

    public static void NotifyAdded(ListerBuildings listerBuildings, Building building) {
        var cache = CacheTable.GetValue(listerBuildings, static _ => new CacheState()).ColonistBuildingsByDef;
        if (cache.TryGetValue(building.def, out var buildings)) {
            buildings.Add(building);
        }
    }

    public static void NotifyRemoved(ListerBuildings listerBuildings, Building building) {
        var cache = CacheTable.GetValue(listerBuildings, static _ => new CacheState()).ColonistBuildingsByDef;
        if (!cache.TryGetValue(building.def, out var buildings)) {
            return;
        }

        var index = buildings.LastIndexOf(building);
        if (index >= 0) {
            buildings.RemoveAt(index);
        }
    }
}