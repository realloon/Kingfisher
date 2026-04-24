#if false
using JetBrains.Annotations;
using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Kingfisher.Features;

[HarmonyPatch(typeof(StorageGroupUtility), nameof(StorageGroupUtility.SetStorageGroup))]
public static class StorageGroupSetStorageGroupPatch {
    [UsedImplicitly]
    public static void Prefix(IStorageGroupMember member, ref StorageGroup? __state) {
        __state = member.Group;
    }

    [UsedImplicitly]
    public static void Postfix(IStorageGroupMember member, StorageGroup? newGroup, StorageGroup? __state) {
        if (__state == newGroup) {
            return;
        }

        ListerMergeablesRewrite.NotifyStorageGroupChanged(member, __state, newGroup);
    }
}

[HarmonyPatch(typeof(StorageGroup), nameof(StorageGroup.RemoveMember))]
public static class StorageGroupRemoveMemberPatch {
    [UsedImplicitly]
    public static void Postfix(StorageGroup __instance) {
        ListerMergeablesRewrite.NotifyStorageGroupMemberRemoved(__instance);
    }
}
#endif