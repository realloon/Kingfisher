using Kingfisher.Prepatching;

namespace Kingfisher.Features;

internal static class ListerThingsRewrite {
    [MethodRewrite(typeof(ListerThings), nameof(ListerThings.Remove))]
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

        for (var i = 0; i < AllGroups.Length; i++) {
            var group = AllGroups[i];
            if (listerThings.use == ListerThingsUse.Region && !group.StoreInRegion()) {
                continue;
            }

            if (!group.Includes(thing.def)) {
                continue;
            }

            RemoveFromTail(listerThings.listsByGroup[i], thing);
            listerThings.stateHashByGroup[(int)group] += 1;
        }

        listerThings.thingListChangedCallbacks?.onThingRemoved?.Invoke(thing);
    }

    # region Helper

    private static readonly ThingRequestGroup[] AllGroups = ThingListGroupHelper.AllGroups;

    private static void RemoveFromTail<T>(List<T> list, T item) {
        var index = list.LastIndexOf(item);
        if (index >= 0) {
            list.RemoveAt(index);
            return;
        }

        list.Remove(item);
    }

    # endregion
}