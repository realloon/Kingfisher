using RimWorld.Planet;
using Verse.AI;

namespace Kingfisher.Features.Thoughts;

internal static class PawnDiedOrDownedThoughtsRewrite {
    public static void RemoveLostThoughts(Pawn pawn) {
        var relations = pawn.relations;
        var canRemoveColonistLost = pawn.IsColonist && !pawn.IsQuestLodger() && !pawn.IsSlave;
        var canRemoveRelationLost = relations is { everSeenByPlayer: true };

        switch (canRemoveColonistLost) {
            case false when !canRemoveRelationLost:
                return;
            case true:
                RemoveColonistLostThoughts(pawn);
                break;
        }

        if (!canRemoveRelationLost) {
            return;
        }

        FillLostRelationThoughtDefs(pawn);
        RemoveRelationLostThoughts(pawn, relations!);

        if (pawn.RaceProps.Humanlike) {
            RemoveOpinionLostThoughts(pawn);
        }
    }

    public static void RemoveResuedRelativeThought(Pawn pawn) {
        var relations = pawn.relations;
        if (relations is not { everSeenByPlayer: true }) {
            return;
        }

        foreach (var relatedPawn in relations.PotentiallyRelatedPawns) {
            if (relatedPawn == null || relatedPawn == pawn || relatedPawn.Dead || relatedPawn.needs?.mood == null) {
                continue;
            }

            relatedPawn.needs.mood.thoughts.memories.RemoveMemoriesOfDefWhereOtherPawnIs(
                ThoughtDefOf.RescuedRelative,
                pawn
            );
        }
    }

    public static void TryGiveDiedThoughts(Pawn victim, DamageInfo? dinfo) {
        try {
            if (PawnGenerator.IsBeingGenerated(victim) || Current.ProgramState != ProgramState.Playing ||
                victim.wasLeftBehindStartingPawn) {
                return;
            }

            if (victim.RaceProps.Humanlike) {
                AddHumanlikeDiedThoughts(victim, dinfo);
            }

            if (victim.relations is { everSeenByPlayer: true }) {
                AddRelationDiedThoughts(victim, dinfo);
            }

            if ((dinfo.HasValue && dinfo.Value.Def.execution) || !victim.IsPrisonerOfColony) {
                return;
            }

            var responsibleColonist = FindResponsibleColonist(victim, dinfo);
            if (responsibleColonist == null) {
                return;
            }

            if (!victim.guilt.IsGuilty && !victim.InAggroMentalState) {
                Find.HistoryEventsManager.RecordEvent(
                    new HistoryEvent(HistoryEventDefOf.InnocentPrisonerDied,
                        responsibleColonist.Named(HistoryEventArgsNames.Doer))
                );
            } else {
                Find.HistoryEventsManager.RecordEvent(
                    new HistoryEvent(HistoryEventDefOf.GuiltyPrisonerDied,
                        responsibleColonist.Named(HistoryEventArgsNames.Doer))
                );
            }

            Find.HistoryEventsManager.RecordEvent(
                new HistoryEvent(HistoryEventDefOf.PrisonerDied,
                    responsibleColonist.Named(HistoryEventArgsNames.Doer))
            );
        } catch (Exception ex) {
            Log.Error("Could not give thoughts: " + ex);
        }
    }

    # region Helper

    private static readonly List<ThoughtDef> LostRelationThoughtDefs = new(16);
    private static readonly List<Pawn> RelatedPawns = new(16);

    private static void RemoveColonistLostThoughts(Pawn pawn) {
        var colonists = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_Colonists;
        foreach (var colonist in colonists) {
            if (colonist == pawn || colonist.needs?.mood == null) {
                continue;
            }

            colonist.needs.mood.thoughts.memories.RemoveMemoriesOfDefWhereOtherPawnIs(ThoughtDefOf.ColonistLost, pawn);
        }
    }

    private static void FillLostRelationThoughtDefs(Pawn pawn) {
        LostRelationThoughtDefs.Clear();

        var relationDefs = DefDatabase<PawnRelationDef>.AllDefsListForReading;
        foreach (var relationDef in relationDefs) {
            var thoughtDef = relationDef.GetGenderSpecificLostThought(pawn);
            if (thoughtDef != null) {
                LostRelationThoughtDefs.Add(thoughtDef);
            }
        }
    }

    private static void RemoveRelationLostThoughts(Pawn pawn, Pawn_RelationsTracker relations) {
        foreach (var relatedPawn in relations.PotentiallyRelatedPawns) {
            if (relatedPawn == null || relatedPawn == pawn || relatedPawn.Dead || relatedPawn.needs?.mood == null ||
                !PawnUtility.ShouldGetThoughtAbout(relatedPawn, pawn)) {
                continue;
            }

            var memories = relatedPawn.needs.mood.thoughts.memories;
            foreach (var thoughtDef in LostRelationThoughtDefs) {
                memories.RemoveMemoriesOfDefWhereOtherPawnIs(thoughtDef, pawn);
            }
        }
    }

    private static void RemoveOpinionLostThoughts(Pawn pawn) {
        var alivePawns = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive;
        foreach (var otherPawn in alivePawns) {
            if (otherPawn == pawn || otherPawn.needs?.mood == null || !otherPawn.RaceProps.IsFlesh ||
                !PawnUtility.ShouldGetThoughtAbout(otherPawn, pawn)) {
                continue;
            }

            var memories = otherPawn.needs.mood.thoughts.memories;
            memories.RemoveMemoriesOfDefWhereOtherPawnIs(ThoughtDefOf.PawnWithGoodOpinionLost, pawn);
            memories.RemoveMemoriesOfDefWhereOtherPawnIs(ThoughtDefOf.PawnWithBadOpinionLost, pawn);
        }
    }

    private static void AddHumanlikeDiedThoughts(Pawn victim, DamageInfo? dinfo) {
        var isExecution = dinfo.HasValue && dinfo.Value.Def.execution;

        if (dinfo.HasValue && dinfo.Value.Def.ExternalViolenceFor(victim) &&
            dinfo.Value.Instigator is Pawn instigator) {
            if (instigator is { Dead: false, needs.mood: not null, story: not null } &&
                instigator != victim &&
                PawnUtility.ShouldGetThoughtAbout(instigator, victim)) {
                AddThought(ThoughtDefOf.KilledHumanlikeBloodlust, instigator);

                if (victim.HostileTo(instigator) && victim.Faction != null && PawnUtility.IsFactionLeader(victim) &&
                    victim.Faction.HostileTo(instigator.Faction)) {
                    AddThought(ThoughtDefOf.DefeatedHostileFactionLeader, instigator, victim);
                }

                if (ModsConfig.BiotechActive && !victim.DevelopmentalStage.Adult() &&
                    instigator.DevelopmentalStage.Adult()) {
                    AddThought(ThoughtDefOf.KilledChild, instigator, victim);
                }
            }
        }

        if (isExecution) {
            return;
        }

        if (victim.IsCaravanMember()) {
            AddWitnessedDeathThoughtsForCaravan(victim);
        } else if (victim.Spawned) {
            AddWitnessedDeathThoughtsForMap(victim);
        }

        if (victim.Faction != Faction.OfPlayer || victim.HostFaction == Faction.OfPlayer || victim.IsQuestLodger() ||
            victim.IsSubhuman || victim.IsSlave) {
            return;
        }

        var playerFactionPawns = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction;
        foreach (var pawn in playerFactionPawns) {
            if (pawn == victim || pawn.needs?.mood == null || IsExcludedSocialFightWitness(pawn, victim) ||
                WasHandledAsWitness(pawn, victim)) {
                continue;
            }

            AddThought(ThoughtDefOf.KnowColonistDied, pawn, victim);
        }
    }

    private static void AddWitnessedDeathThoughtsForCaravan(Pawn victim) {
        var caravan = victim.GetCaravan();
        if (caravan == null) {
            return;
        }

        var pawns = caravan.PawnsListForReading;
        foreach (var t in pawns) {
            TryAddWitnessedDeathThoughts(t, victim);
        }
    }

    private static void AddWitnessedDeathThoughtsForMap(Pawn victim) {
        var map = victim.Map;
        var cellCount = GenRadial.NumCellsInRadius(12f);
        for (var i = 0; i < cellCount; i++) {
            var cell = victim.Position + GenRadial.RadialPattern[i];
            if (!cell.InBounds(map)) {
                continue;
            }

            var things = map.thingGrid.ThingsListAtFast(cell);
            foreach (var t in things) {
                if (t is Pawn pawn) {
                    TryAddWitnessedDeathThoughts(pawn, victim);
                }
            }
        }
    }

    private static void TryAddWitnessedDeathThoughts(Pawn pawn, Pawn victim) {
        if (pawn == victim || pawn.needs?.mood == null || !PawnUtility.ShouldGetThoughtAbout(pawn, victim) ||
            IsExcludedSocialFightWitness(pawn, victim) || !ThoughtUtility.Witnessed(pawn, victim)) {
            return;
        }

        var questLodger = pawn.Faction == Faction.OfPlayer && victim.IsQuestLodger();
        if (pawn.Faction == victim.Faction && !questLodger && !victim.IsSlave) {
            AddThought(ThoughtDefOf.WitnessedDeathAlly, pawn);
        } else if (victim.Faction == null || !victim.Faction.HostileTo(pawn.Faction) || questLodger || victim.IsSlave) {
            AddThought(ThoughtDefOf.WitnessedDeathNonAlly, pawn);
        }

        if (pawn.relations.FamilyByBlood.Contains(victim)) {
            AddThought(ThoughtDefOf.WitnessedDeathFamily, pawn);
        }

        AddThought(ThoughtDefOf.WitnessedDeathBloodlust, pawn);
    }

    private static void AddRelationDiedThoughts(Pawn victim, DamageInfo? dinfo) {
        FillRelatedPawns(victim);

        foreach (var pawn in RelatedPawns) {
            if (!PawnUtility.ShouldGetThoughtAbout(pawn, victim)) {
                continue;
            }

            var thoughtDef = pawn.GetMostImportantRelation(victim)?
                .GetGenderSpecificThought(victim, PawnDiedOrDownedThoughtsKind.Died);
            if (thoughtDef != null) {
                AddThought(thoughtDef, pawn, victim);
            }
        }

        if (dinfo is { Instigator: Pawn instigator } && instigator != victim) {
            foreach (var pawn in RelatedPawns) {
                if (pawn == instigator || !PawnUtility.ShouldGetThoughtAbout(pawn, victim)) {
                    continue;
                }

                var killedThought = pawn.GetMostImportantRelation(victim)?.GetGenderSpecificKilledThought(victim);
                if (killedThought != null) {
                    AddThought(killedThought, pawn, instigator);
                }

                if (!pawn.RaceProps.IsFlesh) {
                    continue;
                }

                var opinion = pawn.relations.OpinionOf(victim);
                switch (opinion) {
                    case >= 20:
                        AddThought(ThoughtDefOf.KilledMyFriend, pawn, instigator, 1f,
                            victim.relations.GetFriendDiedThoughtPowerFactor(opinion));
                        break;
                    case <= -20:
                        AddThought(ThoughtDefOf.KilledMyRival, pawn, instigator, 1f,
                            victim.relations.GetRivalDiedThoughtPowerFactor(opinion));
                        break;
                }
            }
        }

        if (!victim.RaceProps.Humanlike) {
            RelatedPawns.Clear();
            return;
        }

        var alivePawns = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive;
        foreach (var pawn in alivePawns) {
            if (pawn == victim || pawn.needs?.mood == null || !pawn.RaceProps.IsFlesh ||
                !PawnUtility.ShouldGetThoughtAbout(pawn, victim) || !ShouldCheckOpinionThoughts(pawn, victim)) {
                continue;
            }

            var opinion = pawn.relations.OpinionOf(victim);
            switch (opinion) {
                case >= 20:
                    AddThought(ThoughtDefOf.PawnWithGoodOpinionDied, pawn, victim,
                        victim.relations.GetFriendDiedThoughtPowerFactor(opinion));
                    break;
                case <= -20:
                    AddThought(ThoughtDefOf.PawnWithBadOpinionDied, pawn, victim,
                        victim.relations.GetRivalDiedThoughtPowerFactor(opinion));
                    break;
            }
        }

        RelatedPawns.Clear();
    }

    private static void FillRelatedPawns(Pawn victim) {
        RelatedPawns.Clear();
        foreach (var pawn in victim.relations.PotentiallyRelatedPawns) {
            if (pawn?.needs?.mood != null) {
                RelatedPawns.Add(pawn);
            }
        }
    }

    private static bool ShouldCheckOpinionThoughts(Pawn pawn, Pawn victim) {
        if (pawn.Faction == victim.Faction) {
            return true;
        }

        if (pawn.relations.RelatedToAnyoneOrAnyoneRelatedToMe ||
            pawn.needs.mood.thoughts.memories.AnyMemoryConcerns(victim)) {
            return true;
        }

        if (victim.IsCaravanMember()) {
            return pawn.GetCaravan() == victim.GetCaravan();
        }

        return false;
    }

    private static bool IsExcludedSocialFightWitness(Pawn pawn, Pawn victim) {
        return pawn.MentalStateDef == MentalStateDefOf.SocialFighting &&
               ((MentalState_SocialFighting)pawn.MentalState).otherPawn == victim;
    }

    private static bool WasHandledAsWitness(Pawn pawn, Pawn victim) {
        if (pawn == victim || pawn.needs?.mood == null || IsExcludedSocialFightWitness(pawn, victim)) {
            return false;
        }

        if (victim.IsCaravanMember()) {
            return pawn.GetCaravan() == victim.GetCaravan() && ThoughtUtility.Witnessed(pawn, victim);
        }

        return victim.Spawned && pawn.Spawned && pawn.Map == victim.Map && ThoughtUtility.Witnessed(pawn, victim);
    }

    private static void AddThought(ThoughtDef thoughtDef, Pawn addTo, Pawn? otherPawn = null,
        float moodPowerFactor = 1f,
        float opinionOffsetFactor = 1f) {
        new IndividualThoughtToAdd(thoughtDef, addTo, otherPawn, moodPowerFactor, opinionOffsetFactor).Add();
    }

    private static Pawn? FindResponsibleColonist(Pawn victim, DamageInfo? dinfo) {
        if (dinfo is { Instigator: Pawn { IsColonist: not false } instigatorColonist }) {
            return instigatorColonist;
        }

        if (victim.Spawned) {
            var freeColonists = victim.Map.mapPawns.FreeColonistsSpawned;
            Pawn? closestStandingColonist = null;
            var closestStandingDistance = int.MaxValue;
            Pawn? closestColonist = null;
            var closestDistance = int.MaxValue;

            foreach (var pawn in freeColonists) {
                var distance = pawn.Position.DistanceToSquared(victim.Position);

                if (distance < closestDistance) {
                    closestDistance = distance;
                    closestColonist = pawn;
                }

                if (pawn.Downed || distance >= closestStandingDistance) {
                    continue;
                }

                closestStandingDistance = distance;
                closestStandingColonist = pawn;
            }

            return closestStandingColonist ?? closestColonist;
        }

        var worldFreeColonists = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists;
        return worldFreeColonists.Count > 0 ? worldFreeColonists[0] : null;
    }

    # endregion
}
