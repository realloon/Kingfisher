using JetBrains.Annotations;
using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Kingfisher.Patches;

[HarmonyPatch(typeof(ListerThings), nameof(ListerThings.Remove))]
public static class Prefix_ListerThings_Remove {
    [UsedImplicitly]
    public static bool Prefix(ListerThings __instance, Thing t) {
        if (t.def.projectile == null) return true;

        ProjectileListerOptimizer.RemoveProjectile(__instance, t);

        return false;
    }
}

internal static class ProjectileListerOptimizer {
    private static readonly ThingRequestGroup[] AllGroups = ThingListGroupHelper.AllGroups;

    private static readonly AccessTools.FieldRef<ListerThings, Dictionary<ThingDef, List<Thing>>> ListsByDefRef =
        AccessTools.FieldRefAccess<ListerThings, Dictionary<ThingDef, List<Thing>>>("listsByDef");

    private static readonly AccessTools.FieldRef<ListerThings, List<Thing>[]> ListsByGroupRef =
        AccessTools.FieldRefAccess<ListerThings, List<Thing>[]>("listsByGroup");

    private static readonly AccessTools.FieldRef<ListerThings, List<IHaulSource>> HaulSourcesRef =
        AccessTools.FieldRefAccess<ListerThings, List<IHaulSource>>("haulSources");

    private static readonly AccessTools.FieldRef<ListerThings, int[]> StateHashByGroupRef =
        AccessTools.FieldRefAccess<ListerThings, int[]>("stateHashByGroup");

    public static void RemoveProjectile(ListerThings listerThings, Thing thing) {
        if (!ListerThings.EverListable(thing.def, listerThings.use)) return;

        var listsByDef = ListsByDefRef(listerThings);
        if (listsByDef.TryGetValue(thing.def, out var byDefList)) {
            RemoveRecentThing(byDefList, thing);
        }

        if (thing is IHaulSource haulSource) {
            RemoveRecentHaulSource(HaulSourcesRef(listerThings), haulSource);
        }

        var listsByGroup = ListsByGroupRef(listerThings);
        var stateHashByGroup = StateHashByGroupRef(listerThings);
        for (var i = 0; i < AllGroups.Length; i++) {
            var group = AllGroups[i];
            if (listerThings.use == ListerThingsUse.Region && !group.StoreInRegion()) {
                continue;
            }

            if (!group.Includes(thing.def)) continue;

            RemoveRecentThing(listsByGroup[i], thing);

            stateHashByGroup[(int)group] += 1;
        }

        listerThings.thingListChangedCallbacks?.onThingRemoved?.Invoke(thing);
    }

    private static void RemoveRecentThing(List<Thing> list, Thing thing) {
        var index = list.LastIndexOf(thing);
        if (index >= 0) {
            list.RemoveAt(index);
            return;
        }

        list.Remove(thing);
    }

    private static void RemoveRecentHaulSource(List<IHaulSource> list, IHaulSource haulSource) {
        var index = list.LastIndexOf(haulSource);
        if (index >= 0) {
            list.RemoveAt(index);
            return;
        }

        list.Remove(haulSource);
    }
}