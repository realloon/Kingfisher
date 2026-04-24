#if false
using JetBrains.Annotations;
using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Kingfisher.Features;

[HarmonyPatch(typeof(WorkGiver_Grower), nameof(WorkGiver_Grower.PotentialWorkCellsGlobal))]
public static class WorkGiverGrowerPotentialWorkCellsPatch {
    [UsedImplicitly]
    public static bool Prefix(WorkGiver_Grower __instance, Pawn pawn, ref IEnumerable<IntVec3>? __result) {
        if (__instance is not WorkGiver_GrowerSow) {
            return true;
        }

        __result = GrowerSowCellCache.GetPotentialCells(pawn);
        return false;
    }
}

[HarmonyPatch(typeof(MapEvents), nameof(MapEvents.Notify_ThingSpawned))]
public static class MapEventsThingSpawnedGrowerSowPatch {
    [UsedImplicitly]
    public static void Postfix(Thing thing) {
        GrowerSowCellCache.NotifyThingChanged(thing);
    }
}

[HarmonyPatch(typeof(MapEvents), nameof(MapEvents.Notify_ThingDespawned))]
public static class MapEventsThingDespawnedGrowerSowPatch {
    [UsedImplicitly]
    public static void Postfix(Thing thing) {
        GrowerSowCellCache.NotifyThingChanged(thing);
    }
}

[HarmonyPatch(typeof(Zone), nameof(Zone.AddCell))]
public static class ZoneAddCellGrowerSowPatch {
    [UsedImplicitly]
    public static void Postfix(Zone __instance, IntVec3 c) {
        GrowerSowCellCache.NotifyZoneCellChanged(__instance, c);
    }
}

[HarmonyPatch(typeof(Zone), nameof(Zone.RemoveCell))]
public static class ZoneRemoveCellGrowerSowPatch {
    [UsedImplicitly]
    public static void Postfix(Zone __instance, IntVec3 c) {
        GrowerSowCellCache.NotifyZoneCellChanged(__instance, c);
    }
}

[HarmonyPatch(typeof(Building_PlantGrower), nameof(Building_PlantGrower.SetPlantDefToGrow))]
public static class BuildingPlantGrowerSetPlantDefPatch {
    [UsedImplicitly]
    public static void Postfix(Building_PlantGrower __instance) {
        if (!__instance.Spawned) {
            return;
        }

        GrowerSowCellCache.NotifyPlantDefChanged(__instance, __instance.Map);
    }
}

[HarmonyPatch(typeof(Zone_Growing), nameof(Zone_Growing.SetPlantDefToGrow))]
public static class ZoneGrowingSetPlantDefPatch {
    [UsedImplicitly]
    public static void Postfix(Zone_Growing __instance) {
        GrowerSowCellCache.NotifyPlantDefChanged(__instance, __instance.Map);
    }
}
#endif