using PurePatcher.Annotations;

namespace Kingfisher.Features;

public static class ListerThingsRewrite {
    [ReplaceMethod(typeof(ListerThings), nameof(ListerThings.Remove))]
    public static void Remove(ListerThings listerThings, Thing thing) {
        if (!ListerThings.EverListable(thing.def, listerThings.use)) {
            return;
        }

        if (listerThings.listsByDef.TryGetValue(thing.def, out var byDefList)) {
            RemoveFromTail(byDefList, thing);
        }

        if (thing is IHaulSource haulSource) {
            RemoveFromTail(listerThings.haulSources, haulSource);
        }

        var allGroups = ThingListGroupHelper.AllGroups;
        for (var i = 0; i < allGroups.Length; i++) {
            var group = allGroups[i];
            if (listerThings.use == ListerThingsUse.Region && !group.StoreInRegion()) {
                continue;
            }

            if (!group.Includes(thing.def)) {
                continue;
            }

            var groupList = listerThings.listsByGroup[i];
            if (groupList == null) {
                continue;
            }

            RemoveFromTail(groupList, thing);
            listerThings.stateHashByGroup[(int)group] += 1;
        }

        listerThings.thingListChangedCallbacks?.onThingRemoved?.Invoke(thing);
    }

    # region Helper

    private static void RemoveFromTail<T>(List<T> list, T item) {
        var index = list.LastIndexOf(item);
        if (index < 0) return;

        list.RemoveAt(index);
    }

    # endregion
}