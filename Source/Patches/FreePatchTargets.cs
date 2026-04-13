using Verse.AI;

namespace Kingfisher.Patches;

public static class FreePatchTargets {
    private static readonly ThingRequestGroup[] AllGroups = ThingListGroupHelper.AllGroups;

    public static IAttackTarget? BestAttackTarget(IAttackTargetSearcher searcher, TargetScanFlags flags,
        Predicate<Thing>? validator, float minDist, float maxDist, IntVec3 locus, float maxTravelRadiusFromLocus,
        bool canBashDoors, bool canTakeTargetsCloserThanEffectiveMinRange, bool canBashFences, bool onlyRanged) =>
        AttackTargetFinderOptimizer.BestAttackTarget(
            searcher,
            flags,
            validator,
            minDist,
            maxDist,
            locus,
            maxTravelRadiusFromLocus,
            canBashDoors,
            canTakeTargetsCloserThanEffectiveMinRange,
            canBashFences,
            onlyRanged
        );

    public static void RemoveThing(ListerThings listerThings, Thing thing) {
        if (!ListerThings.EverListable(thing.def, listerThings.use)) {
            return;
        }

        if (thing.def.projectile != null) {
            RemoveProjectile(listerThings, thing);
            return;
        }

        if (listerThings.listsByDef.TryGetValue(thing.def, out var byDefList)) {
            byDefList.Remove(thing);
        }

        if (thing is IHaulSource haulSource) {
            listerThings.haulSources.Remove(haulSource);
        }

        for (var i = 0; i < AllGroups.Length; i++) {
            var group = AllGroups[i];
            if (listerThings.use == ListerThingsUse.Region && !group.StoreInRegion()) {
                continue;
            }

            if (!group.Includes(thing.def)) {
                continue;
            }

            listerThings.listsByGroup[i].Remove(thing);
            listerThings.stateHashByGroup[(int)group] += 1;
        }

        listerThings.thingListChangedCallbacks?.onThingRemoved?.Invoke(thing);
    }

    public static List<Building> AllBuildingsColonistOfDef(ListerBuildings listerBuildings, ThingDef def) =>
        ColonistBuildingDefCache.CopyBuildingsOfDef(listerBuildings, def);

    public static bool ColonistsHaveBuilding(ListerBuildings listerBuildings, ThingDef def) =>
        ColonistBuildingDefCache.GetOrBuild(listerBuildings, def).Count > 0;

    public static bool ColonistsHaveBuildingWithPowerOn(ListerBuildings listerBuildings, ThingDef def) {
        var buildings = ColonistBuildingDefCache.GetOrBuild(listerBuildings, def);
        foreach (var building in buildings) {
            var compPowerTrader = building.PowerTraderComp();
            if (compPowerTrader is { PowerOn: false }) {
                continue;
            }

            return true;
        }

        return false;
    }

    public static void RemoveLostThoughts(Pawn pawn) => PawnDiedOrDownedThoughtsOptimizer.RemoveLostThoughts(pawn);

    public static void RemoveResuedRelativeThought(Pawn pawn) =>
        PawnDiedOrDownedThoughtsOptimizer.RemoveResuedRelativeThought(pawn);

    private static void RemoveProjectile(ListerThings listerThings, Thing thing) {
        if (listerThings.listsByDef.TryGetValue(thing.def, out var byDefList)) {
            RemoveRecentThing(byDefList, thing);
        }

        if (thing is IHaulSource haulSource) {
            RemoveRecentHaulSource(listerThings.haulSources, haulSource);
        }

        for (var i = 0; i < AllGroups.Length; i++) {
            var group = AllGroups[i];
            if (listerThings.use == ListerThingsUse.Region && !group.StoreInRegion()) {
                continue;
            }

            if (!group.Includes(thing.def)) {
                continue;
            }

            RemoveRecentThing(listerThings.listsByGroup[i], thing);
            listerThings.stateHashByGroup[(int)group] += 1;
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
