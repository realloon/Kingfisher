#if false
// Copyright (c) 2021 bradson
// Copyright (c) 2026 Vortex
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
// SPDX-License-Identifier: MPL-2.0

using UnityEngine;
using Verse.AI;
using Kingfisher.Prepatching;

namespace Kingfisher.Features;

public static class WorkGiverMergeRewrite {
    [MethodRewrite(typeof(WorkGiver_Merge), nameof(WorkGiver_Merge.JobOnThing))]
    public static Job? JobOnThing(WorkGiver_Merge giver, Pawn pawn, Thing t, bool forced = false) {
        if (t.stackCount == t.def.stackLimit) {
            return null;
        }

        if (!HaulAIUtility.PawnCanAutomaticallyHaul(pawn, t, forced)) {
            return null;
        }

        var slotGroup = t.GetSlotGroup();
        if (slotGroup == null) {
            return null;
        }

        if (!pawn.CanReserve(t.Position, 1, -1, null, forced)) {
            return null;
        }

        if (ListerMergeablesRewrite.TryFindMergeTarget(t.Map.listerMergeables, pawn, t, forced, out var mergeTarget) &&
            mergeTarget != null) {
            return CreateJob(t, mergeTarget);
        }

        var effectiveGroup = slotGroup.StorageGroup is { } storageGroup ? (ISlotGroup)storageGroup : slotGroup;
        foreach (var heldThing in effectiveGroup.HeldThings) {
            if (heldThing == t ||
                !heldThing.CanStackWith(t) ||
                (!forced && heldThing.stackCount < t.stackCount) ||
                heldThing.stackCount >= heldThing.def.stackLimit ||
                heldThing.IsForbidden(pawn.Faction) ||
                !heldThing.Position.InAllowedArea(pawn) ||
                !pawn.CanReserve(heldThing.Position, 1, -1, null, forced) ||
                !pawn.CanReserve(heldThing) ||
                !heldThing.Position.IsValidStorageFor(heldThing.Map, t) ||
                heldThing.Position.ContainsStaticFire(heldThing.Map)) {
                continue;
            }

            return CreateJob(t, heldThing);
        }

        JobFailReason.Is("NoMergeTarget".Translate());
        return null;
    }

    private static Job CreateJob(Thing source, Thing mergeTarget) {
        var job = JobMaker.MakeJob(JobDefOf.HaulToCell, source, mergeTarget.Position);
        job.count = Mathf.Min(mergeTarget.def.stackLimit - mergeTarget.stackCount, source.stackCount);
        job.haulMode = HaulMode.ToCellStorage;
        return job;
    }
}
#endif