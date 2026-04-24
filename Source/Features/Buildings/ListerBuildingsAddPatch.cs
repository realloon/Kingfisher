// Copyright (c) 2021 bradson
// Copyright (c) 2026 Vortex
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
// SPDX-License-Identifier: MPL-2.0

using JetBrains.Annotations;
using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Kingfisher.Features;

[HarmonyPatch(typeof(ListerBuildings), nameof(ListerBuildings.Add))]
public static class ListerBuildingsAddPatch {
    [UsedImplicitly]
    public static void Postfix(ListerBuildings __instance, Building b) {
        if (!ListerBuildingsRewrite.ShouldTrack(b)) {
            return;
        }

        ListerBuildingsRewrite.NotifyAdded(__instance, b);
    }
}