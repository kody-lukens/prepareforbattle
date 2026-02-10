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
            HashSet<string> handledSteps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string step in settings.ActionOrder)
            {
                if (string.IsNullOrWhiteSpace(step) || handledSteps.Contains(step))
                {
                    continue;
                }

                handledSteps.Add(step);
                switch (step)
                {
                    case "Drug":
                        AddDrugJobs(pawn, settings, jobs, autoGoJuice, ref autoGoJuiceQueued);
                        break;
                    case "Food":
                        AddFoodJobs(pawn, settings, jobs);
                        break;
                    case "Weapon":
                        AddWeaponJobs(pawn, settings, jobs);
                        break;
                    case "Armor":
                        AddArmorJobs(pawn, settings, jobs);
                        break;
                }
            }

            if (jobs.Count == 0)
            {
                return;
            }

            EnqueueJobs(pawn, jobs);
        }

        private static void AddDrugJobs(Pawn pawn, PrepareForBattleSettings settings, List<Job> jobs, bool autoGoJuice, ref bool autoGoJuiceQueued)
        {
            if (!settings.EnableDrugUse)
            {
                return;
            }

            if (autoGoJuice)
            {
                if (TryIngestDrug(pawn, new[] { "GoJuice" }, settings, out Job autoGoJuiceJob))
                {
                    jobs.Add(autoGoJuiceJob);
                    autoGoJuiceQueued = true;
                    return;
                }

                Messages.Message($"No Go-juice found for {pawn.LabelShort}.", MessageTypeDefOf.RejectInput, false);
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
        }

        private static void AddFoodJobs(Pawn pawn, PrepareForBattleSettings settings, List<Job> jobs)
        {
            if (!settings.EnableFoodEating)
            {
                return;
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
        }

        private static void AddWeaponJobs(Pawn pawn, PrepareForBattleSettings settings, List<Job> jobs)
        {
            if (!settings.EnableWeaponEquip || pawn.equipment?.Primary != null)
            {
                return;
            }

            if (TryEquipWeapon(pawn, settings, out Job weaponJob))
            {
                jobs.Add(weaponJob);
            }
        }

        private static void AddArmorJobs(Pawn pawn, PrepareForBattleSettings settings, List<Job> jobs)
        {
            if (!settings.EnableArmorEquip)
            {
                return;
            }

            List<Job> armorJobs = BuildArmorJobs(pawn, settings);
            if (armorJobs.Count > 0)
            {
                jobs.AddRange(armorJobs);
            }

            if (settings.EnableUtilityEquip && !HasUtility(pawn))
            {
                if (TryEquipUtility(pawn, settings, out Job utilityJob))
                {
                    jobs.Add(utilityJob);
                }
            }
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


        private static bool TryEquipWeapon(Pawn pawn, PrepareForBattleSettings settings, out Job job)
        {
            Thing weapon = FindWeaponTarget(pawn, settings);
            if (weapon == null)
            {
                job = null;
                return false;
            }

            job = JobMaker.MakeJob(JobDefOf.Equip, weapon);
            return true;
        }

        private static Thing FindWeaponTarget(Pawn pawn, PrepareForBattleSettings settings)
        {
            if (settings.InventoryFirst)
            {
                Thing inventoryWeapon = FindWeaponInInventory(pawn);
                if (inventoryWeapon != null)
                {
                    return inventoryWeapon;
                }
            }

            if (settings.SearchOnMap)
            {
                Thing mapWeapon = FindWeaponOnMap(pawn, settings);
                if (mapWeapon != null)
                {
                    return mapWeapon;
                }
            }

            if (!settings.InventoryFirst)
            {
                return FindWeaponInInventory(pawn);
            }

            return null;
        }

        private static Thing FindWeaponInInventory(Pawn pawn)
        {
            if (pawn.inventory?.innerContainer == null)
            {
                return null;
            }

            Thing bestWeapon = null;
            float bestValue = -1f;
            foreach (Thing thing in pawn.inventory.innerContainer)
            {
                if (thing?.def == null || !thing.def.IsRangedWeapon)
                {
                    continue;
                }

                if (!EquipmentUtility.CanEquip(thing, pawn))
                {
                    continue;
                }

                float value = thing.def.GetStatValueAbstract(StatDefOf.MarketValue);
                if (value > bestValue)
                {
                    bestValue = value;
                    bestWeapon = thing;
                }
            }

            return bestWeapon;
        }

        private static Thing FindWeaponOnMap(Pawn pawn, PrepareForBattleSettings settings)
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

            ThingRequest request = ThingRequest.ForGroup(ThingRequestGroup.Weapon);
            TraverseParms traverse = TraverseParms.For(pawn);
            return GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                request,
                PathEndMode.ClosestTouch,
                traverse,
                radius,
                thing => thing?.def != null &&
                         thing.def.IsRangedWeapon &&
                         !thing.IsForbidden(pawn) &&
                         EquipmentUtility.CanEquip(thing, pawn),
                null,
                9999,
                0,
                false,
                RegionType.Set_Passable,
                false,
                false);
        }

        private static bool TryEquipArmor(Pawn pawn, PrepareForBattleSettings settings, out Job job)
        {
            Apparel apparel = FindArmorTarget(pawn, settings);
            if (apparel == null)
            {
                job = null;
                return false;
            }

            job = JobMaker.MakeJob(JobDefOf.Wear, apparel);
            return true;
        }

        private static List<Job> BuildArmorJobs(Pawn pawn, PrepareForBattleSettings settings)
        {
            List<Job> jobs = new List<Job>();
            if (pawn == null)
            {
                return jobs;
            }

            List<Apparel> worn = pawn.apparel?.WornApparel?.ToList() ?? new List<Apparel>();
            HashSet<Apparel> usedThings = new HashSet<Apparel>();
            List<Apparel> candidates = GetArmorCandidates(pawn, settings);

            foreach (Apparel candidate in candidates.OrderByDescending(GetArmorScore))
            {
                if (usedThings.Contains(candidate))
                {
                    continue;
                }

                if (!CanWearArmorBasic(pawn, candidate, settings))
                {
                    continue;
                }

                List<Apparel> conflicts = worn
                    .Where(w => w != null && !ApparelUtility.CanWearTogether(candidate.def, w.def, pawn.RaceProps.body))
                    .ToList();

                float candidateScore = GetArmorScore(candidate);
                float conflictsScore = conflicts.Sum(GetArmorScore);

                if (conflicts.Count == 0 || candidateScore > conflictsScore + 0.01f)
                {
                    jobs.Add(JobMaker.MakeJob(JobDefOf.Wear, candidate));
                    usedThings.Add(candidate);
                    foreach (Apparel conflict in conflicts)
                    {
                        worn.Remove(conflict);
                    }
                    worn.Add(candidate);
                }
            }

            return jobs;
        }

        private static Apparel FindArmorTarget(Pawn pawn, PrepareForBattleSettings settings)
        {
            List<ThingDef> wornDefs = pawn.apparel?.WornApparel?.Select(a => a.def).ToList() ?? new List<ThingDef>();
            return FindArmorTargetWithWorn(pawn, settings, wornDefs, null);
        }

        private static Apparel FindArmorTargetWithWorn(Pawn pawn, PrepareForBattleSettings settings, List<ThingDef> wornDefs, HashSet<Thing> usedThings)
        {
            if (settings.InventoryFirst)
            {
                Apparel inventoryArmor = FindArmorInInventory(pawn, settings, wornDefs, usedThings);
                if (inventoryArmor != null)
                {
                    return inventoryArmor;
                }
            }

            if (settings.SearchOnMap)
            {
                Apparel mapArmor = FindArmorOnMap(pawn, settings, wornDefs, usedThings);
                if (mapArmor != null)
                {
                    return mapArmor;
                }
            }

            if (!settings.InventoryFirst)
            {
                return FindArmorInInventory(pawn, settings, wornDefs, usedThings);
            }

            return null;
        }

        private static Apparel FindArmorInInventory(Pawn pawn, PrepareForBattleSettings settings, List<ThingDef> wornDefs, HashSet<Thing> usedThings)
        {
            if (pawn.inventory?.innerContainer == null)
            {
                return null;
            }

            Apparel bestArmor = null;
            float bestValue = -1f;
            foreach (Thing thing in pawn.inventory.innerContainer)
            {
                if (usedThings != null && usedThings.Contains(thing))
                {
                    continue;
                }

                Apparel apparel = thing as Apparel;
                if (apparel == null || !IsArmorApparel(apparel))
                {
                    continue;
                }

                if (!CanWearArmorBasic(pawn, apparel, settings))
                {
                    continue;
                }

                float value = apparel.def.GetStatValueAbstract(StatDefOf.MarketValue);
                if (value > bestValue)
                {
                    bestValue = value;
                    bestArmor = apparel;
                }
            }

            return bestArmor;
        }

        private static Apparel FindArmorOnMap(Pawn pawn, PrepareForBattleSettings settings, List<ThingDef> wornDefs, HashSet<Thing> usedThings)
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

            ThingRequest request = ThingRequest.ForGroup(ThingRequestGroup.Apparel);
            TraverseParms traverse = TraverseParms.For(pawn);
            Thing thing = GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                request,
                PathEndMode.ClosestTouch,
                traverse,
                radius,
                candidate =>
                {
                    Apparel apparel = candidate as Apparel;
                    return apparel != null &&
                           IsArmorApparel(apparel) &&
                           !apparel.IsForbidden(pawn) &&
                           (usedThings == null || !usedThings.Contains(apparel)) &&
                           CanWearArmorBasic(pawn, apparel, settings);
                },
                null,
                9999,
                0,
                false,
                RegionType.Set_Passable,
                false,
                false);

            return thing as Apparel;
        }

        private static bool IsArmorApparel(Apparel apparel)
        {
            if (apparel?.def?.apparel == null)
            {
                return false;
            }

            float sharp = apparel.GetStatValue(StatDefOf.ArmorRating_Sharp);
            float blunt = apparel.GetStatValue(StatDefOf.ArmorRating_Blunt);
            return sharp > 0f || blunt > 0f;
        }

        private static float GetArmorScore(Apparel apparel)
        {
            if (apparel == null)
            {
                return 0f;
            }

            float sharp = apparel.GetStatValue(StatDefOf.ArmorRating_Sharp);
            float blunt = apparel.GetStatValue(StatDefOf.ArmorRating_Blunt);
            float heat = apparel.GetStatValue(StatDefOf.ArmorRating_Heat);
            return sharp * 1.2f + blunt + heat * 0.5f;
        }

        private static bool CanWearArmorBasic(Pawn pawn, Apparel apparel, PrepareForBattleSettings settings)
        {
            ThingDef def = apparel?.def;
            if (def == null)
            {
                return false;
            }

            if (!ApparelUtility.HasPartsToWear(pawn, def))
            {
                return false;
            }

            CompBiocodable biocoded = apparel.TryGetComp<CompBiocodable>();
            if (biocoded != null && biocoded.Biocoded && !CompBiocodable.IsBiocodedFor(apparel, pawn))
            {
                return false;
            }

            if (settings.RespectClothingRestrictions)
            {
                ApparelPolicy policy = pawn.outfits?.CurrentApparelPolicy;
                if (policy?.filter != null && !policy.filter.Allows(def))
                {
                    return false;
                }
            }

            return true;
        }

        private static List<Apparel> GetArmorCandidates(Pawn pawn, PrepareForBattleSettings settings)
        {
            List<Apparel> candidates = new List<Apparel>();

            if (settings.InventoryFirst)
            {
                AddArmorFromInventory(pawn, settings, candidates);
            }

            if (settings.SearchOnMap)
            {
                AddArmorFromMap(pawn, settings, candidates);
            }

            if (!settings.InventoryFirst)
            {
                AddArmorFromInventory(pawn, settings, candidates);
            }

            return candidates;
        }

        private static bool TryEquipUtility(Pawn pawn, PrepareForBattleSettings settings, out Job job)
        {
            Apparel utility = FindUtilityTarget(pawn, settings);
            if (utility == null)
            {
                job = null;
                return false;
            }

            job = JobMaker.MakeJob(JobDefOf.Wear, utility);
            return true;
        }

        private static Apparel FindUtilityTarget(Pawn pawn, PrepareForBattleSettings settings)
        {
            if (settings.InventoryFirst)
            {
                Apparel inventoryUtility = FindUtilityInInventory(pawn, settings);
                if (inventoryUtility != null)
                {
                    return inventoryUtility;
                }
            }

            if (settings.SearchOnMap)
            {
                Apparel mapUtility = FindUtilityOnMap(pawn, settings);
                if (mapUtility != null)
                {
                    return mapUtility;
                }
            }

            if (!settings.InventoryFirst)
            {
                return FindUtilityInInventory(pawn, settings);
            }

            return null;
        }

        private static Apparel FindUtilityInInventory(Pawn pawn, PrepareForBattleSettings settings)
        {
            if (pawn.inventory?.innerContainer == null)
            {
                return null;
            }

            Apparel best = null;
            float bestValue = -1f;
            foreach (Thing thing in pawn.inventory.innerContainer)
            {
                Apparel apparel = thing as Apparel;
                if (!IsUtilityApparel(apparel))
                {
                    continue;
                }

                if (!CanWearUtility(pawn, apparel, settings))
                {
                    continue;
                }

                float value = apparel.def.GetStatValueAbstract(StatDefOf.MarketValue);
                if (value > bestValue)
                {
                    bestValue = value;
                    best = apparel;
                }
            }

            return best;
        }

        private static Apparel FindUtilityOnMap(Pawn pawn, PrepareForBattleSettings settings)
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

            ThingRequest request = ThingRequest.ForGroup(ThingRequestGroup.Apparel);
            TraverseParms traverse = TraverseParms.For(pawn);
            Thing thing = GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                request,
                PathEndMode.ClosestTouch,
                traverse,
                radius,
                candidate =>
                {
                    Apparel apparel = candidate as Apparel;
                    return IsUtilityApparel(apparel) &&
                           !apparel.IsForbidden(pawn) &&
                           CanWearUtility(pawn, apparel, settings);
                },
                null,
                9999,
                0,
                false,
                RegionType.Set_Passable,
                false,
                false);

            return thing as Apparel;
        }

        private static bool HasUtility(Pawn pawn)
        {
            if (pawn.apparel?.WornApparel == null)
            {
                return false;
            }

            return pawn.apparel.WornApparel.Any(IsUtilityApparel);
        }

        private static bool IsUtilityApparel(Apparel apparel)
        {
            return apparel?.def?.apparel?.layers?.Contains(ApparelLayerDefOf.Belt) == true;
        }

        private static bool CanWearUtility(Pawn pawn, Apparel apparel, PrepareForBattleSettings settings)
        {
            if (apparel?.def == null)
            {
                return false;
            }

            if (!ApparelUtility.HasPartsToWear(pawn, apparel.def))
            {
                return false;
            }

            CompBiocodable biocoded = apparel.TryGetComp<CompBiocodable>();
            if (biocoded != null && biocoded.Biocoded && !CompBiocodable.IsBiocodedFor(apparel, pawn))
            {
                return false;
            }

            if (settings.RespectClothingRestrictions)
            {
                ApparelPolicy policy = pawn.outfits?.CurrentApparelPolicy;
                if (policy?.filter != null && !policy.filter.Allows(apparel.def))
                {
                    return false;
                }
            }

            if (pawn.apparel?.WornApparel != null)
            {
                foreach (Apparel worn in pawn.apparel.WornApparel)
                {
                    if (!ApparelUtility.CanWearTogether(apparel.def, worn.def, pawn.RaceProps.body))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static void AddArmorFromInventory(Pawn pawn, PrepareForBattleSettings settings, List<Apparel> candidates)
        {
            if (pawn.inventory?.innerContainer == null)
            {
                return;
            }

            foreach (Thing thing in pawn.inventory.innerContainer)
            {
                Apparel apparel = thing as Apparel;
                if (apparel == null || !IsArmorApparel(apparel))
                {
                    continue;
                }

                if (candidates.Contains(apparel))
                {
                    continue;
                }

                if (!CanWearArmorBasic(pawn, apparel, settings))
                {
                    continue;
                }

                candidates.Add(apparel);
            }
        }

        private static void AddArmorFromMap(Pawn pawn, PrepareForBattleSettings settings, List<Apparel> candidates)
        {
            if (pawn.Map == null)
            {
                return;
            }

            float radius = settings.SearchRadiusTiles;
            if (radius <= 0f)
            {
                return;
            }

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
                    Apparel apparel = thing as Apparel;
                    if (apparel == null || !IsArmorApparel(apparel))
                    {
                        continue;
                    }

                    if (apparel.IsForbidden(pawn))
                    {
                        continue;
                    }

                    if (candidates.Contains(apparel))
                    {
                        continue;
                    }

                    if (!CanWearArmorBasic(pawn, apparel, settings))
                    {
                        continue;
                    }

                    candidates.Add(apparel);
                }
            }
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
