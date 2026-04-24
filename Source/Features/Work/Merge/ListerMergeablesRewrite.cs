#if false
// Copyright (c) 2021 bradson
// Copyright (c) 2026 Vortex
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
// SPDX-License-Identifier: MPL-2.0

using System.Reflection;
using Kingfisher.Prepatching;
using Prepatcher;
using Verse.AI;

namespace Kingfisher.Features;

public static class ListerMergeablesRewrite {
    private static readonly FieldInfo MapField =
        typeof(ListerMergeables).GetField("map", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [MethodRewrite(typeof(ListerMergeables), nameof(ListerMergeables.ThingsPotentiallyNeedingMerging))]
    public static List<Thing> ThingsPotentiallyNeedingMerging(ListerMergeables listerMergeables) {
        EnsureInitialized(listerMergeables);
        return listerMergeables.MergeCandidates();
    }

    [MethodRewrite(typeof(ListerMergeables), nameof(ListerMergeables.Notify_Spawned))]
    public static void Notify_Spawned(ListerMergeables listerMergeables, Thing t) {
        EnsureInitialized(listerMergeables);
        RefreshThingCore(listerMergeables, t);
    }

    [MethodRewrite(typeof(ListerMergeables), nameof(ListerMergeables.Notify_DeSpawned))]
    public static void Notify_DeSpawned(ListerMergeables listerMergeables, Thing t) {
        EnsureInitialized(listerMergeables);
        RemoveThingCore(listerMergeables, t);
    }

    [MethodRewrite(typeof(ListerMergeables), nameof(ListerMergeables.Notify_Unforbidden))]
    public static void Notify_Unforbidden(ListerMergeables listerMergeables, Thing t) {
        EnsureInitialized(listerMergeables);
        RefreshThingCore(listerMergeables, t);
    }

    [MethodRewrite(typeof(ListerMergeables), nameof(ListerMergeables.Notify_Forbidden))]
    public static void Notify_Forbidden(ListerMergeables listerMergeables, Thing t) {
        EnsureInitialized(listerMergeables);
        RemoveThingCore(listerMergeables, t);
    }

    [MethodRewrite(typeof(ListerMergeables), nameof(ListerMergeables.Notify_SlotGroupChanged))]
    public static void Notify_SlotGroupChanged(ListerMergeables listerMergeables, SlotGroup sg) {
        EnsureInitialized(listerMergeables);

        var cellsList = sg.CellsList;
        if (cellsList == null) {
            return;
        }

        foreach (var t in cellsList) {
            RecalcAllInCell(listerMergeables, t);
        }
    }

    [MethodRewrite(typeof(ListerMergeables), nameof(ListerMergeables.Notify_ThingStackChanged))]
    public static void Notify_ThingStackChanged(ListerMergeables listerMergeables, Thing t) {
        EnsureInitialized(listerMergeables);
        RefreshThingCore(listerMergeables, t);
    }

    [MethodRewrite(typeof(ListerMergeables), nameof(ListerMergeables.RecalcAllInCell))]
    public static void RecalcAllInCell(ListerMergeables listerMergeables, IntVec3 c) {
        EnsureInitialized(listerMergeables);

        var thingList = c.GetThingList(GetMap(listerMergeables));
        foreach (var t in thingList) {
            RefreshThingCore(listerMergeables, t);
        }
    }

    internal static bool TryFindMergeTarget(ListerMergeables listerMergeables, Pawn pawn, Thing source, bool forced,
        out Thing? mergeTarget) {
        EnsureInitialized(listerMergeables);

        if (!listerMergeables.MergeMemberships().TryGetValue(source, out var membership) ||
            !listerMergeables.MergeBuckets().TryGetValue(membership.Group, out var bucketsByKey) ||
            !bucketsByKey.TryGetValue(membership.Key, out var bucket)) {
            mergeTarget = null;
            return false;
        }

        var things = bucket.Things;
        foreach (var candidate in things) {
            if (candidate == source ||
                candidate.Destroyed ||
                !candidate.Spawned ||
                !ShouldTrack(candidate) ||
                (!forced && candidate.stackCount < source.stackCount)) {
                continue;
            }

            if (candidate.IsForbidden(pawn.Faction) ||
                !candidate.Position.InAllowedArea(pawn) ||
                !pawn.CanReserve(candidate.Position, 1, -1, null, forced) ||
                !pawn.CanReserve(candidate) ||
                !candidate.Position.IsValidStorageFor(candidate.Map, source) ||
                candidate.Position.ContainsStaticFire(candidate.Map)) {
                continue;
            }

            mergeTarget = candidate;
            return true;
        }

        mergeTarget = null;
        return false;
    }

    internal static void NotifyStorageGroupChanged(IStorageGroupMember member, StorageGroup? oldGroup,
        StorageGroup? newGroup) {
        var map = member.Map ?? oldGroup?.Map ?? newGroup?.Map;
        if (map == null) {
            return;
        }

        var listerMergeables = map.listerMergeables;
        EnsureInitialized(listerMergeables);

        if (oldGroup != null) {
            RefreshEffectiveGroup(listerMergeables, oldGroup);
        }

        if (newGroup != null && newGroup != oldGroup) {
            RefreshEffectiveGroup(listerMergeables, newGroup);
        }

        if (member is not ISlotGroupParent slotGroupParent) {
            return;
        }

        var slotGroup = slotGroupParent.GetSlotGroup();
        if (slotGroup != null) {
            RefreshEffectiveGroup(listerMergeables, GetEffectiveGroup(slotGroup));
        }
    }

    internal static void NotifyStorageGroupMemberRemoved(StorageGroup storageGroup) {
        var map = storageGroup.Map;
        if (map == null) {
            return;
        }

        var listerMergeables = map.listerMergeables;
        EnsureInitialized(listerMergeables);
        RefreshEffectiveGroup(listerMergeables, storageGroup);
    }

    #region Helper

    private static void EnsureInitialized(ListerMergeables listerMergeables) {
        if (listerMergeables.MergeIndexInitialized()) {
            return;
        }

        listerMergeables.MergeIndexInitialized() = true;
        ClearState(listerMergeables);

        var allThings = GetMap(listerMergeables).listerThings.AllThings;
        foreach (var t in allThings) {
            RefreshThingCore(listerMergeables, t);
        }
    }

    private static void ClearState(ListerMergeables listerMergeables) {
        listerMergeables.MergeBuckets().Clear();
        listerMergeables.MergeMemberships().Clear();
        listerMergeables.MergeCandidates().Clear();
        listerMergeables.MergeCandidateIndices().Clear();
    }

    private static void RefreshEffectiveGroup(ListerMergeables listerMergeables, ISlotGroup effectiveGroup) {
        foreach (var heldThing in effectiveGroup.HeldThings) {
            RefreshThingCore(listerMergeables, heldThing);
        }
    }

    private static void RefreshThingCore(ListerMergeables listerMergeables, Thing t) {
        RemoveThingCore(listerMergeables, t);

        if (!ShouldTrack(t)) {
            return;
        }

        var slotGroup = t.GetSlotGroup();
        var effectiveGroup = slotGroup == null ? null : GetEffectiveGroup(slotGroup);
        if (effectiveGroup == null) {
            return;
        }

        var bucketsByGroup = listerMergeables.MergeBuckets();
        if (!bucketsByGroup.TryGetValue(effectiveGroup, out var bucketsByKey)) {
            bucketsByKey = [];
            bucketsByGroup.Add(effectiveGroup, bucketsByKey);
        }

        var key = new MergeBucketKey(t.def, t.Stuff);
        if (!bucketsByKey.TryGetValue(key, out var bucket)) {
            bucket = new MergeBucket();
            bucketsByKey.Add(key, bucket);
        }

        bucket.Things.Add(t);
        listerMergeables.MergeMemberships().Add(t, new MergeMembership(effectiveGroup, key));
        RefreshBucketCandidates(listerMergeables, bucket);
    }

    private static void RemoveThingCore(ListerMergeables listerMergeables, Thing t) {
        if (!listerMergeables.MergeMemberships().TryGetValue(t, out var membership)) {
            return;
        }

        listerMergeables.MergeMemberships().Remove(t);

        if (!listerMergeables.MergeBuckets().TryGetValue(membership.Group, out var bucketsByKey) ||
            !bucketsByKey.TryGetValue(membership.Key, out var bucket)) {
            RemoveCandidate(listerMergeables, t);
            return;
        }

        var index = bucket.Things.LastIndexOf(t);
        if (index >= 0) {
            bucket.Things.RemoveAt(index);
        }

        RemoveCandidate(listerMergeables, t);

        if (bucket.Things.Count == 0) {
            bucketsByKey.Remove(membership.Key);
            if (bucketsByKey.Count == 0) {
                listerMergeables.MergeBuckets().Remove(membership.Group);
            }

            return;
        }

        RefreshBucketCandidates(listerMergeables, bucket);
    }

    private static void RefreshBucketCandidates(ListerMergeables listerMergeables, MergeBucket bucket) {
        var things = bucket.Things;
        foreach (var t in things) {
            RemoveCandidate(listerMergeables, t);
        }

        if (things.Count < 2) {
            return;
        }

        var maxStackCount = 0;
        var maxStackCountOccurrences = 0;

        foreach (var t in things) {
            var stackCount = t.stackCount;
            if (stackCount > maxStackCount) {
                maxStackCount = stackCount;
                maxStackCountOccurrences = 1;
                continue;
            }

            if (stackCount == maxStackCount) {
                maxStackCountOccurrences++;
            }
        }

        foreach (var thing in things) {
            if (maxStackCountOccurrences == 1 && thing.stackCount == maxStackCount) {
                continue;
            }

            AddCandidate(listerMergeables, thing);
        }
    }

    private static void AddCandidate(ListerMergeables listerMergeables, Thing t) {
        var candidateIndices = listerMergeables.MergeCandidateIndices();
        if (candidateIndices.ContainsKey(t)) {
            return;
        }

        var mergeCandidates = listerMergeables.MergeCandidates();
        candidateIndices.Add(t, mergeCandidates.Count);
        mergeCandidates.Add(t);
    }

    private static void RemoveCandidate(ListerMergeables listerMergeables, Thing t) {
        var candidateIndices = listerMergeables.MergeCandidateIndices();
        if (!candidateIndices.TryGetValue(t, out var index)) {
            return;
        }

        var mergeCandidates = listerMergeables.MergeCandidates();
        var lastIndex = mergeCandidates.Count - 1;
        var lastThing = mergeCandidates[lastIndex];

        mergeCandidates[index] = lastThing;
        candidateIndices[lastThing] = index;

        mergeCandidates.RemoveAt(lastIndex);
        candidateIndices.Remove(t);
    }

    private static bool ShouldTrack(Thing t) {
        if (t.def.category != ThingCategory.Item ||
            t.Destroyed ||
            !t.Spawned ||
            t.IsForbidden(Faction.OfPlayer) ||
            t.GetSlotGroup() == null ||
            t.stackCount == t.def.stackLimit) {
            return false;
        }

        return true;
    }

    private static ISlotGroup GetEffectiveGroup(SlotGroup slotGroup) =>
        slotGroup.StorageGroup is { } storageGroup ? storageGroup : slotGroup;

    private static Map GetMap(ListerMergeables listerMergeables) => (Map)MapField.GetValue(listerMergeables)!;

    [PrepatcherField]
    [ValueInitializer(nameof(CreateBuckets))]
    private static extern Dictionary<ISlotGroup, Dictionary<MergeBucketKey, MergeBucket>> MergeBuckets(
        this ListerMergeables target);

    [PrepatcherField]
    [ValueInitializer(nameof(CreateMemberships))]
    private static extern Dictionary<Thing, MergeMembership> MergeMemberships(this ListerMergeables target);

    [PrepatcherField]
    [ValueInitializer(nameof(CreateCandidates))]
    private static extern List<Thing> MergeCandidates(this ListerMergeables target);

    [PrepatcherField]
    [ValueInitializer(nameof(CreateCandidateIndices))]
    private static extern Dictionary<Thing, int> MergeCandidateIndices(this ListerMergeables target);

    [PrepatcherField]
    [ValueInitializer(nameof(CreateInitialized))]
    private static extern ref bool MergeIndexInitialized(this ListerMergeables target);

    private static Dictionary<ISlotGroup, Dictionary<MergeBucketKey, MergeBucket>> CreateBuckets() => [];

    private static Dictionary<Thing, MergeMembership> CreateMemberships() => [];

    private static List<Thing> CreateCandidates() => [];

    private static Dictionary<Thing, int> CreateCandidateIndices() => [];

    private static bool CreateInitialized() => false;

    private readonly struct MergeMembership(ISlotGroup group, MergeBucketKey key) {
        public ISlotGroup Group { get; } = group;

        public MergeBucketKey Key { get; } = key;
    }

    private readonly struct MergeBucketKey(ThingDef def, ThingDef? stuff) : IEquatable<MergeBucketKey> {
        private ThingDef Def { get; } = def;

        private ThingDef? Stuff { get; } = stuff;

        public bool Equals(MergeBucketKey other) => Def == other.Def && Stuff == other.Stuff;

        public override bool Equals(object? obj) => obj is MergeBucketKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Def, Stuff);
    }

    private sealed class MergeBucket {
        public List<Thing> Things { get; } = [];
    }

    #endregion
}
#endif