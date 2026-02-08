using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace PrepareForBattle
{
    public class DrugEntry : IExposable
    {
        public string DefName;
        public bool Enabled = true;

        public DrugEntry()
        {
        }

        public DrugEntry(string defName, bool enabled)
        {
            DefName = defName;
            Enabled = enabled;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref DefName, "DefName");
            Scribe_Values.Look(ref Enabled, "Enabled", true);
        }
    }

    public class PrepareForBattleSettings : ModSettings
    {
        public static readonly string[] DefaultDrugDefs = { "WakeUp", "PsychiteTea", "Beer", "Ambrosia" };

        public float HungerThreshold = 0.6f;
        public float RestThreshold = 0.6f;
        public float RecreationThreshold = 0.6f;
        public bool RespectDrugPolicy = true;
        public int SearchRadiusTiles = 45;
        public bool SearchOnMap = true;
        public bool InventoryFirst = true;
        public bool HasInitializedDefaults;
        public List<DrugEntry> AllowedDrugs = new List<DrugEntry>();

        public override void ExposeData()
        {
            Scribe_Values.Look(ref HungerThreshold, "HungerThreshold", 0.6f);
            Scribe_Values.Look(ref RestThreshold, "RestThreshold", 0.6f);
            Scribe_Values.Look(ref RecreationThreshold, "RecreationThreshold", 0.6f);
            Scribe_Values.Look(ref RespectDrugPolicy, "RespectDrugPolicy", true);
            Scribe_Values.Look(ref SearchRadiusTiles, "SearchRadiusTiles", 45);
            Scribe_Values.Look(ref SearchOnMap, "SearchOnMap", true);
            Scribe_Values.Look(ref InventoryFirst, "InventoryFirst", true);
            Scribe_Values.Look(ref HasInitializedDefaults, "HasInitializedDefaults", false);
            Scribe_Collections.Look(ref AllowedDrugs, "AllowedDrugEntries", LookMode.Deep);

            List<string> legacyAllowed = null;
            Scribe_Collections.Look(ref legacyAllowed, "AllowedDrugs", LookMode.Value);

            if (AllowedDrugs == null)
            {
                AllowedDrugs = new List<DrugEntry>();
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit && legacyAllowed != null && legacyAllowed.Count > 0 && AllowedDrugs.Count == 0)
            {
                AllowedDrugs = legacyAllowed.Select(defName => new DrugEntry(defName, true)).ToList();
            }
        }

        public void EnsureDefaultsInitialized()
        {
            if (HasInitializedDefaults && AllowedDrugs != null && AllowedDrugs.Count > 0)
            {
                return;
            }

            AllowedDrugs = BuildDefaultDrugEntries();
            HasInitializedDefaults = true;
        }

        private static List<DrugEntry> BuildDefaultDrugEntries()
        {
            List<ThingDef> coreDrugs = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(def => def.IsDrug && def.ingestible != null && def.modContentPack != null && def.modContentPack.IsCoreMod)
                .ToList();

            List<DrugEntry> result = new List<DrugEntry>();
            HashSet<string> added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string[] preferredOrder =
            {
                "WakeUp",
                "GoJuice",
                "PsychiteTea",
                "Beer",
                "Ambrosia",
                "SmokeleafJoint",
                "Yayo",
                "Flake"
            };

            foreach (string defName in preferredOrder)
            {
                ThingDef def = coreDrugs.FirstOrDefault(d => string.Equals(d.defName, defName, StringComparison.OrdinalIgnoreCase));
                if (def != null && added.Add(def.defName))
                {
                    result.Add(new DrugEntry(def.defName, true));
                }
            }

            foreach (ThingDef def in coreDrugs.OrderBy(d => d.defName))
            {
                if (added.Add(def.defName))
                {
                    result.Add(new DrugEntry(def.defName, true));
                }
            }

            return result;
        }
    }
}
