using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace PrepareForBattle
{
    public static class BattlePrepUtility
    {
        public static void TryPrepare(Pawn pawn)
        {
            if (pawn == null || pawn.Dead)
            {
                return;
            }

            PrepareForBattleSettings settings = PrepareForBattleMod.Settings;
            if (settings == null)
            {
                return;
            }

            List<Job> jobs = new List<Job>();
            bool autoGoJuice = IsAutoGoJuiceEnabled(pawn);
            bool autoGoJuiceQueued = false;

            if (autoGoJuice)
            {
                if (TryIngestDrug(pawn, new[] { "GoJuice" }, settings, out Job autoGoJuiceJob))
                {
                    jobs.Add(autoGoJuiceJob);
                    autoGoJuiceQueued = true;
                }
                else
                {
                    Messages.Message($"No Go-juice found for {pawn.LabelShort}.", MessageTypeDefOf.RejectInput, false);
                }
            }

            if (pawn.needs?.food != null && pawn.needs.food.CurLevelPercentage < settings.HungerThreshold)
            {
                if (TryIngestBestFood(pawn, settings, out Job job))
                {
                    jobs.Add(job);
                }
                else
                {
                    Messages.Message($"No allowed food found for {pawn.LabelShort}.", MessageTypeDefOf.RejectInput, false);
                }
            }

            bool canUseDefaultDrugs = !autoGoJuice || !autoGoJuiceQueued;
            bool drugQueued = autoGoJuiceQueued;
            List<string> enabledDrugs = GetEnabledDrugDefNames(settings);

            if (canUseDefaultDrugs && !drugQueued && pawn.needs?.rest != null && pawn.needs.rest.CurLevelPercentage < settings.RestThreshold)
            {
                if (TryIngestDrug(pawn, enabledDrugs, settings, out Job job))
                {
                    jobs.Add(job);
                    drugQueued = true;
                }
                else
                {
                    Messages.Message($"No allowed rest drug found for {pawn.LabelShort}.", MessageTypeDefOf.RejectInput, false);
                }
            }

            if (canUseDefaultDrugs && !drugQueued && pawn.needs?.joy != null && pawn.needs.joy.CurLevelPercentage < settings.RecreationThreshold)
            {
                if (TryIngestDrug(pawn, enabledDrugs, settings, out Job job))
                {
                    jobs.Add(job);
                }
                else
                {
                    Messages.Message($"No allowed recreation drug found for {pawn.LabelShort}.", MessageTypeDefOf.RejectInput, false);
                }
            }

            if (jobs.Count == 0)
            {
                return;
            }

            EnqueueJobs(pawn, jobs);
        }

        private static bool TryIngestBestFood(Pawn pawn, PrepareForBattleSettings settings, out Job job)
        {
            Thing target = FindFoodTarget(pawn, settings);
            job = CreateIngestJob(target);
            return job != null;
        }

        private static bool TryIngestDrug(Pawn pawn, IEnumerable<string> drugDefNames, PrepareForBattleSettings settings, out Job job)
        {
            Thing target = FindDrugTarget(pawn, drugDefNames, settings);
            job = CreateIngestJob(target);
            return job != null;
        }

        private static List<string> GetEnabledDrugDefNames(PrepareForBattleSettings settings)
        {
            if (settings?.AllowedDrugs == null)
            {
                return new List<string>();
            }

            return settings.AllowedDrugs
                .Where(entry => entry != null && entry.Enabled && !string.IsNullOrWhiteSpace(entry.DefName))
                .Select(entry => entry.DefName)
                .ToList();
        }

        private static bool IsAutoGoJuiceEnabled(Pawn pawn)
        {
            return AutoGoJuiceComponent.GetForCurrentGame()?.IsEnabled(pawn) ?? false;
        }

        private static Thing FindFoodTarget(Pawn pawn, PrepareForBattleSettings settings)
        {
            if (settings.InventoryFirst)
            {
                Thing inventoryFood = FindBestFoodInInventory(pawn);
                if (inventoryFood != null)
                {
                    return inventoryFood;
                }
            }

            if (settings.SearchOnMap)
            {
                Thing mapFood = FindNearestFoodOnMap(pawn, settings);
                if (mapFood != null)
                {
                    return mapFood;
                }
            }

            if (!settings.InventoryFirst)
            {
                return FindBestFoodInInventory(pawn);
            }

            return null;
        }

        private static Thing FindDrugTarget(Pawn pawn, IEnumerable<string> drugDefNames, PrepareForBattleSettings settings)
        {
            List<string> orderedDefs = drugDefNames?.ToList() ?? new List<string>();
            if (orderedDefs.Count == 0)
            {
                return null;
            }

            if (settings.InventoryFirst)
            {
                Thing inventoryDrug = FindDrugInInventory(pawn, orderedDefs);
                if (inventoryDrug != null)
                {
                    return inventoryDrug;
                }
            }

            if (settings.SearchOnMap)
            {
                Thing mapDrug = FindDrugOnMap(pawn, orderedDefs, settings);
                if (mapDrug != null)
                {
                    return mapDrug;
                }
            }

            if (!settings.InventoryFirst)
            {
                return FindDrugInInventory(pawn, orderedDefs);
            }

            return null;
        }

        private static Thing FindBestFoodInInventory(Pawn pawn)
        {
            Thing bestFood = null;
            float bestNutrition = -1f;

            if (pawn.inventory?.innerContainer == null)
            {
                return null;
            }

            foreach (Thing thing in pawn.inventory.innerContainer)
            {
                if (!IsValidFoodForPawn(pawn, thing))
                {
                    continue;
                }

                float nutrition = thing.def.ingestible.CachedNutrition;
                if (nutrition <= 0f)
                {
                    continue;
                }

                if (nutrition > bestNutrition)
                {
                    bestNutrition = nutrition;
                    bestFood = thing;
                }
            }

            return bestFood;
        }

        private static Thing FindNearestFoodOnMap(Pawn pawn, PrepareForBattleSettings settings)
        {
            if (pawn.Map == null)
            {
                return null;
            }

            float radius = settings.SearchRadiusTiles;
            if (radius <= 0f)
            {
                return null;
            }

            ThingRequest request = ThingRequest.ForGroup(ThingRequestGroup.FoodSource);
            TraverseParms traverse = TraverseParms.For(pawn);
            return GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                request,
                PathEndMode.ClosestTouch,
                traverse,
                radius,
                thing => IsValidFoodForPawn(pawn, thing) && !thing.IsForbidden(pawn),
                null,
                9999,
                0,
                false,
                RegionType.Set_Passable,
                false,
                false);
        }

        private static Thing FindDrugInInventory(Pawn pawn, List<string> orderedDefs)
        {
            if (pawn.inventory?.innerContainer == null)
            {
                return null;
            }

            foreach (string defName in orderedDefs)
            {
                Thing drug = pawn.inventory.innerContainer.FirstOrDefault(t =>
                    t?.def?.ingestible != null &&
                    t.def.IsDrug &&
                    string.Equals(t.def.defName, defName, StringComparison.OrdinalIgnoreCase) &&
                    IsDrugAllowed(pawn, t.def));

                if (drug != null)
                {
                    return drug;
                }
            }

            return null;
        }

        private static Thing FindDrugOnMap(Pawn pawn, List<string> orderedDefs, PrepareForBattleSettings settings)
        {
            if (pawn.Map == null)
            {
                return null;
            }

            float radius = settings.SearchRadiusTiles;
            if (radius <= 0f)
            {
                return null;
            }

            TraverseParms traverse = TraverseParms.For(pawn);
            foreach (string defName in orderedDefs)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                if (def == null)
                {
                    continue;
                }

                ThingRequest request = ThingRequest.ForDef(def);
                Thing drug = GenClosest.ClosestThingReachable(
                    pawn.Position,
                    pawn.Map,
                    request,
                    PathEndMode.ClosestTouch,
                    traverse,
                    radius,
                    thing => thing != null &&
                             !thing.IsForbidden(pawn) &&
                             thing.def.IsDrug &&
                             thing.def.ingestible != null &&
                             IsDrugAllowed(pawn, thing.def),
                    null,
                    9999,
                    0,
                    false,
                    RegionType.Set_Passable,
                    false,
                    false);

                if (drug != null)
                {
                    return drug;
                }
            }

            Log.Message(BuildDrugSearchFailureLog(pawn, orderedDefs, settings));
            return null;
        }

        private static bool IsDrugAllowed(Pawn pawn, ThingDef drugDef)
        {
            if (!PrepareForBattleMod.Settings.RespectDrugPolicy)
            {
                return true;
            }

            DrugPolicy policy = pawn.drugs?.CurrentPolicy;
            if (policy == null)
            {
                return true;
            }

            DrugPolicyEntry entry = policy[drugDef];
            if (entry == null)
            {
                return true;
            }

            return entry.allowedForJoy || entry.allowedForAddiction || entry.allowScheduled;
        }

        private static bool IsValidFoodForPawn(Pawn pawn, Thing thing)
        {
            if (thing?.def?.ingestible == null)
            {
                return false;
            }

            if (thing.def.IsDrug)
            {
                return false;
            }

            if (!FoodUtility.WillEat(pawn, thing, pawn, false, false))
            {
                return false;
            }

            FoodPolicy policy = pawn.foodRestriction?.GetCurrentRespectedRestriction();
            if (policy != null && !policy.Allows(thing.def))
            {
                return false;
            }

            return true;
        }

        private static Job CreateIngestJob(Thing thing)
        {
            if (thing == null)
            {
                return null;
            }

            Job job = JobMaker.MakeJob(JobDefOf.Ingest, thing);
            job.count = 1;
            return job;
        }

        private static void EnqueueJobs(Pawn pawn, List<Job> jobs)
        {
            if (pawn.jobs == null || jobs == null || jobs.Count == 0)
            {
                return;
            }

            if (!pawn.jobs.TryTakeOrderedJob(jobs[0], JobTag.Misc))
            {
                return;
            }

            for (int i = 1; i < jobs.Count; i++)
            {
                pawn.jobs.jobQueue.EnqueueLast(jobs[i]);
            }

            Job waitJob = JobMaker.MakeJob(JobDefOf.Wait_Combat);
            waitJob.expiryInterval = 60;
            waitJob.checkOverrideOnExpire = true;
            pawn.jobs.jobQueue.EnqueueLast(waitJob);
        }

        private static string BuildDrugSearchFailureLog(Pawn pawn, List<string> orderedDefs, PrepareForBattleSettings settings)
        {
            if (pawn.Map == null)
            {
                return $"[PrepareForBattle] Drug search failed for {pawn.LabelShort}: no map.";
            }

            HashSet<string> allowed = new HashSet<string>(orderedDefs, StringComparer.OrdinalIgnoreCase);
            List<string> details = new List<string>();
            float radius = settings.SearchRadiusTiles;

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(pawn.Position, radius, true))
            {
                if (!cell.InBounds(pawn.Map))
                {
                    continue;
                }

                List<Thing> things = pawn.Map.thingGrid.ThingsListAtFast(cell);
                if (things == null || things.Count == 0)
                {
                    continue;
                }

                foreach (Thing thing in things)
                {
                    if (thing?.def == null || !thing.def.IsDrug || thing.def.ingestible == null)
                    {
                        continue;
                    }

                    bool isAllowed = allowed.Contains(thing.def.defName);
                    bool isForbidden = thing.IsForbidden(pawn);
                    bool reachable = pawn.CanReach(thing, PathEndMode.ClosestTouch, Danger.Deadly);
                    bool policyAllowed = IsDrugAllowed(pawn, thing.def);

                    if (isAllowed && !isForbidden && reachable && policyAllowed)
                    {
                        continue;
                    }

                    details.Add($"{thing.def.defName} (allowed={isAllowed}, policy={policyAllowed}, forbidden={isForbidden}, reachable={reachable})");
                }
            }

            if (details.Count == 0)
            {
                return $"[PrepareForBattle] Drug search failed for {pawn.LabelShort}: no drug candidates within {radius} tiles.";
            }

            return $"[PrepareForBattle] Drug search failed for {pawn.LabelShort} within {radius} tiles. Candidates: {string.Join(", ", details)}";
        }

        public static string BuildGizmoTooltip()
        {
            return "Eat food & take a drug if Food, Rest, or Recreation are below designated thresholds (see mod settings).";
        }
    }
}
