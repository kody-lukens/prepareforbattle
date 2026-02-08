using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace PrepareForBattle
{
    public class PrepareForBattleMod : Mod
    {
        public static PrepareForBattleSettings Settings;
        private string _searchRadiusBuffer;

        public PrepareForBattleMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<PrepareForBattleSettings>();
            Settings.EnsureDefaultsInitialized();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label($"Hunger threshold: {Settings.HungerThreshold:P0}");
            Settings.HungerThreshold = listing.Slider(Settings.HungerThreshold, 0f, 1f);

            listing.Gap();
            listing.Label($"Rest threshold: {Settings.RestThreshold:P0}");
            Settings.RestThreshold = listing.Slider(Settings.RestThreshold, 0f, 1f);

            listing.Gap();
            listing.Label($"Recreation threshold: {Settings.RecreationThreshold:P0}");
            Settings.RecreationThreshold = listing.Slider(Settings.RecreationThreshold, 0f, 1f);

            listing.Gap();
            listing.CheckboxLabeled("Respect drug policy", ref Settings.RespectDrugPolicy);
            listing.Label(Settings.RespectDrugPolicy
                ? "Only use drugs that this pawn's current drug policy allows."
                : "Ignore drug policy (still requires the drug to be enabled in Allowed drugs).");

            listing.GapLine();
            listing.Label("Search radius (tiles):");
            Rect radiusRect = listing.GetRect(Text.LineHeight);
            Widgets.IntEntry(radiusRect, ref Settings.SearchRadiusTiles, ref _searchRadiusBuffer, 1);
            Settings.SearchRadiusTiles = Mathf.Clamp(Settings.SearchRadiusTiles, 1, 250);

            listing.Gap();
            listing.CheckboxLabeled("Search on map when not in inventory", ref Settings.SearchOnMap);
            listing.CheckboxLabeled("Inventory first", ref Settings.InventoryFirst);

            listing.GapLine();
            DrawDrugAddMenu(listing);
            listing.Gap();
            DrawAllowedDrugsSection(listing);

            listing.End();
            Settings.Write();
        }

        public override string SettingsCategory()
        {
            return "Prepare For Battle";
        }

        private void DrawAllowedDrugsSection(Listing_Standard listing)
        {
            Settings.EnsureDefaultsInitialized();

            listing.Label("Allowed drugs (priority order)");

            bool changed = false;
            for (int i = 0; i < Settings.AllowedDrugs.Count; i++)
            {
                DrugEntry entry = Settings.AllowedDrugs[i];
                string defName = entry?.DefName ?? string.Empty;
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                string label = def?.label?.CapitalizeFirst() ?? defName;
                Rect row = listing.GetRect(Text.LineHeight);
                float checkboxWidth = 24f;
                float iconSize = row.height;
                float buttonWidth = 38f;
                float removeWidth = 62f;
                float gap = 4f;
                float buttonsWidth = checkboxWidth + iconSize + buttonWidth * 2f + removeWidth + gap * 4f;

                Rect labelRect = new Rect(row.x, row.y, row.width - buttonsWidth, row.height);
                Rect checkboxRect = new Rect(labelRect.xMax + gap, row.y, checkboxWidth, row.height);
                Rect iconRect = new Rect(checkboxRect.xMax + gap, row.y, iconSize, iconSize);
                Rect upRect = new Rect(iconRect.xMax + gap, row.y, buttonWidth, row.height);
                Rect downRect = new Rect(upRect.xMax + gap, row.y, buttonWidth, row.height);
                Rect removeRect = new Rect(downRect.xMax + gap, row.y, removeWidth, row.height);

                Widgets.Label(labelRect, $"{label} ({defName})");

                bool enabled = entry != null && entry.Enabled;
                Widgets.Checkbox(checkboxRect.position, ref enabled, checkboxRect.width);
                if (entry != null && entry.Enabled != enabled)
                {
                    entry.Enabled = enabled;
                    changed = true;
                }

                if (def != null)
                {
                    Widgets.DefIcon(iconRect, def);
                }

                if (Widgets.ButtonText(upRect, "Up") && i > 0)
                {
                    DrugEntry temp = Settings.AllowedDrugs[i - 1];
                    Settings.AllowedDrugs[i - 1] = Settings.AllowedDrugs[i];
                    Settings.AllowedDrugs[i] = temp;
                    changed = true;
                }

                if (Widgets.ButtonText(downRect, "Down") && i < Settings.AllowedDrugs.Count - 1)
                {
                    DrugEntry temp = Settings.AllowedDrugs[i + 1];
                    Settings.AllowedDrugs[i + 1] = Settings.AllowedDrugs[i];
                    Settings.AllowedDrugs[i] = temp;
                    changed = true;
                }

                if (Widgets.ButtonText(removeRect, "Remove"))
                {
                    Settings.AllowedDrugs.RemoveAt(i);
                    changed = true;
                }

                if (changed)
                {
                    break;
                }
            }

            if (Settings.AllowedDrugs.Count == 0)
            {
                listing.Label("No drugs enabled.");
            }
        }

        private void DrawDrugAddMenu(Listing_Standard listing)
        {
            Rect buttonRect = listing.GetRect(Text.LineHeight);
            if (!Widgets.ButtonText(buttonRect, "Add drug..."))
            {
                return;
            }

            List<ThingDef> drugDefs = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(def => def.IsDrug && def.ingestible != null)
                .OrderBy(def => def.label)
                .ToList();

            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (ThingDef def in drugDefs)
            {
                string label = $"{def.label.CapitalizeFirst()} ({def.defName})";
                options.Add(new FloatMenuOption(label, () => AddDrug(def.defName), def));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void AddDrug(string defName)
        {
            Settings.EnsureDefaultsInitialized();
            DrugEntry existing = Settings.AllowedDrugs.FirstOrDefault(entry =>
                entry != null && string.Equals(entry.DefName, defName, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.Enabled = true;
                int existingIndex = Settings.AllowedDrugs.IndexOf(existing);
                if (existingIndex > 0)
                {
                    Settings.AllowedDrugs.RemoveAt(existingIndex);
                    Settings.AllowedDrugs.Insert(0, existing);
                }
                return;
            }

            Settings.AllowedDrugs.Insert(0, new DrugEntry(defName, true));
        }
    }
}
