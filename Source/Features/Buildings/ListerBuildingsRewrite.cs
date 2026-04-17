// Copyright (c) 2021 bradson
// Copyright (c) 2026 Vortex
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
// SPDX-License-Identifier: MPL-2.0

using Kingfisher.Prepatching;
using Prepatcher;

namespace Kingfisher.Features.Buildings;

internal static class ListerBuildingsRewrite {
    public static List<Building> AllBuildingsColonistOfDef(ListerBuildings listerBuildings, ThingDef def) {
        var resultBuffer = listerBuildings.ColonistBuildingsOfDefResult();
        resultBuffer.Clear();
        resultBuffer.AddRange(GetOrBuild(listerBuildings, def));
        return resultBuffer;
    }

    public static bool ColonistsHaveBuilding(ListerBuildings listerBuildings, ThingDef def) {
        return GetOrBuild(listerBuildings, def).Count > 0;
    }

    public static bool ColonistsHaveBuildingWithPowerOn(ListerBuildings listerBuildings, ThingDef def) {
        var buildings = GetOrBuild(listerBuildings, def);
        foreach (var building in buildings) {
            var compPowerTrader = building.PowerTraderComp();
            if (compPowerTrader is { PowerOn: false }) {
                continue;
            }

            return true;
        }

        return false;
    }

    # region Helper

    internal static bool ShouldTrack(Building building) =>
        building.Faction == Faction.OfPlayer && building.def.building is not { isNaturalRock: true };

    internal static void NotifyAdded(ListerBuildings listerBuildings, Building building) {
        var cache = listerBuildings.ColonistBuildingsByDef();
        if (cache.TryGetValue(building.def, out var buildings)) {
            buildings.Add(building);
        }
    }

    internal static void NotifyRemoved(ListerBuildings listerBuildings, Building building) {
        var cache = listerBuildings.ColonistBuildingsByDef();
        if (!cache.TryGetValue(building.def, out var buildings)) {
            return;
        }

        var index = buildings.LastIndexOf(building);
        if (index >= 0) {
            buildings.RemoveAt(index);
        }
    }

    private static List<Building> GetOrBuild(ListerBuildings listerBuildings, ThingDef def) {
        var cache = listerBuildings.ColonistBuildingsByDef();
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

    [PrepatcherField]
    [ValueInitializer(nameof(CreateCache))]
    private static extern ref Dictionary<ThingDef, List<Building>> ColonistBuildingsByDef(this ListerBuildings target);

    [PrepatcherField]
    [ValueInitializer(nameof(CreateResultBuffer))]
    private static extern List<Building> ColonistBuildingsOfDefResult(this ListerBuildings target);

    private static Dictionary<ThingDef, List<Building>> CreateCache() => [];

    private static List<Building> CreateResultBuffer() => [];

    # endregion
}