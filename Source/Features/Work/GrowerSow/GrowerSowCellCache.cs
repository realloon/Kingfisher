#if false
using JetBrains.Annotations;
using System.Runtime.CompilerServices;
using Verse.AI;

namespace Kingfisher.Features;

public static class GrowerSowCellCache {
    private static readonly ConditionalWeakTable<Map, MapState> StateByMap = [];

    public static IEnumerable<IntVec3> GetPotentialCells(Pawn pawn) {
        var map = pawn.Map;
        var state = StateByMap.GetOrCreateValue(map);
        state.EnsureInitialized(map);

        var maxDanger = pawn.NormalMaxDanger();
        foreach (var candidateSettable in state.CandidateSettables.Values) {
            if (!CanUseSettable(candidateSettable.Settable, pawn, maxDanger)) {
                continue;
            }

            var cells = candidateSettable.Cells;
            foreach (var t in cells) {
                yield return t;
            }
        }
    }

    public static void NotifyThingChanged(Thing thing) {
        if (!AffectsSowCandidates(thing)) {
            return;
        }

        var map = thing.Map;
        if (map == null) {
            return;
        }

        var state = StateByMap.GetOrCreateValue(map);
        state.EnsureInitialized(map);
        state.RecalcRect(map, thing.OccupiedRect());
    }

    public static void NotifyZoneCellChanged(Zone zone, IntVec3 cell) {
        if (zone is not IPlantToGrowSettable) {
            return;
        }

        var map = zone.Map;
        var state = StateByMap.GetOrCreateValue(map);
        state.EnsureInitialized(map);
        state.RecalcCell(map, cell);
    }

    public static void NotifyPlantDefChanged(IPlantToGrowSettable settable, Map map) {
        var state = StateByMap.GetOrCreateValue(map);
        state.EnsureInitialized(map);
        state.RecalcSettable(map, settable);
    }

    private static bool AffectsSowCandidates(Thing thing) {
        return thing.def.category == ThingCategory.Plant ||
               thing.def.BlocksPlanting() ||
               thing is IPlantToGrowSettable;
    }

    private static bool CanUseSettable(IPlantToGrowSettable settable, Pawn pawn, Danger maxDanger) {
        if (!settable.CanAcceptSowNow()) {
            return false;
        }

        switch (settable) {
            case Building_PlantGrower building:
                if (building.IsForbidden(pawn) || building.IsBurning()) {
                    return false;
                }

                return pawn.CanReach(building, PathEndMode.OnCell, maxDanger);

            case Zone_Growing zone:
                if (!zone.allowSow || zone.cells.Count == 0 || zone.ContainsStaticFire) {
                    return false;
                }

                return pawn.CanReach(zone.Cells[0], PathEndMode.OnCell, maxDanger);

            default:
                return false;
        }
    }

    [UsedImplicitly]
    private sealed class MapState {
        public readonly Dictionary<IPlantToGrowSettable, CandidateSettable> CandidateSettables = [];

        private readonly Dictionary<IntVec3, IPlantToGrowSettable> _ownerByCell = [];
        private bool _initialized;

        public void EnsureInitialized(Map map) {
            if (_initialized) {
                return;
            }

            _initialized = true;
            CandidateSettables.Clear();
            _ownerByCell.Clear();

            var zones = map.zoneManager.AllZones;
            foreach (var t1 in zones) {
                if (t1 is not Zone_Growing zoneGrowing) {
                    continue;
                }

                foreach (var t in zoneGrowing.cells) {
                    RecalcCell(map, t);
                }
            }

            var buildings = map.listerBuildings.allBuildingsColonist;
            foreach (var t in buildings) {
                if (t is not Building_PlantGrower plantGrower) {
                    continue;
                }

                RecalcRect(map, plantGrower.OccupiedRect());
            }
        }

        public void RecalcRect(Map map, CellRect rect) {
            foreach (var cell in rect) {
                RecalcCell(map, cell);
            }
        }

        public void RecalcSettable(Map map, IPlantToGrowSettable settable) {
            switch (settable) {
                case Building_PlantGrower building:
                    RecalcRect(map, building.OccupiedRect());
                    break;
                case Zone_Growing zone:
                    foreach (var t in zone.cells) {
                        RecalcCell(map, t);
                    }

                    break;
            }
        }

        public void RecalcCell(Map map, IntVec3 cell) {
            if (_ownerByCell.TryGetValue(cell, out var previousOwner)) {
                RemoveCell(previousOwner, cell);
            }

            var settable = cell.GetPlantToGrowSettable(map);
            if (settable == null || !IsPotentialSowCell(map, cell, settable)) {
                return;
            }

            if (!CandidateSettables.TryGetValue(settable, out var candidateSettable)) {
                candidateSettable = new CandidateSettable(settable);
                CandidateSettables.Add(settable, candidateSettable);
            }

            _ownerByCell.Add(cell, settable);
            candidateSettable.CellIndices.Add(cell, candidateSettable.Cells.Count);
            candidateSettable.Cells.Add(cell);
        }

        private void RemoveCell(IPlantToGrowSettable owner, IntVec3 cell) {
            _ownerByCell.Remove(cell);

            if (!CandidateSettables.TryGetValue(owner, out var candidateSettable) ||
                !candidateSettable.CellIndices.TryGetValue(cell, out var index)) {
                return;
            }

            var cells = candidateSettable.Cells;
            var lastIndex = cells.Count - 1;
            var lastCell = cells[lastIndex];

            cells[index] = lastCell;
            candidateSettable.CellIndices[lastCell] = index;

            cells.RemoveAt(lastIndex);
            candidateSettable.CellIndices.Remove(cell);

            if (cells.Count == 0) {
                CandidateSettables.Remove(owner);
            }
        }

        private static bool IsPotentialSowCell(Map map, IntVec3 cell, IPlantToGrowSettable settable) {
            var wantedPlantDef = settable.GetPlantDefToGrow();
            if (wantedPlantDef == null) {
                return false;
            }

            var thingList = cell.GetThingList(map);
            foreach (var t in thingList) {
                if (t.def == wantedPlantDef) {
                    return false;
                }
            }

            return true;
        }
    }

    private sealed class CandidateSettable(IPlantToGrowSettable settable) {
        public readonly IPlantToGrowSettable Settable = settable;

        public readonly List<IntVec3> Cells = [];

        public readonly Dictionary<IntVec3, int> CellIndices = [];
    }
}
#endif