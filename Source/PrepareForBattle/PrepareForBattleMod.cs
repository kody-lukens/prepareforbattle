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
            float gap = 12f;
            float columnWidth = (inRect.width - gap) * 0.5f;
            Rect leftRect = new Rect(inRect.x, inRect.y, columnWidth, inRect.height);
            Rect rightRect = new Rect(leftRect.xMax + gap, inRect.y, columnWidth, inRect.height);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(leftRect);

            listing.Label($"Hunger threshold: {Settings.HungerThreshold:P0}");
            Settings.HungerThreshold = listing.Slider(Settings.HungerThreshold, 0f, 1f);

            listing.Label($"Rest threshold: {Settings.RestThreshold:P0}");
            Settings.RestThreshold = listing.Slider(Settings.RestThreshold, 0f, 1f);

            listing.Label($"Recreation threshold: {Settings.RecreationThreshold:P0}");
            Settings.RecreationThreshold = listing.Slider(Settings.RecreationThreshold, 0f, 1f);

            listing.Gap();
            listing.CheckboxLabeled("Respect drug policy", ref Settings.RespectDrugPolicy);

            listing.GapLine();
            listing.CheckboxLabeled("Enable food eating", ref Settings.EnableFoodEating);
            listing.CheckboxLabeled("Enable drug use", ref Settings.EnableDrugUse);
            listing.CheckboxLabeled("Enable weapon equip", ref Settings.EnableWeaponEquip);
            listing.CheckboxLabeled("Enable armor equip", ref Settings.EnableArmorEquip);
            listing.CheckboxLabeled("Enable utility equip", ref Settings.EnableUtilityEquip);
            listing.CheckboxLabeled("Respect clothing restrictions", ref Settings.RespectClothingRestrictions);

            listing.GapLine();
            listing.Label("Search radius (tiles):");
            Rect radiusRect = listing.GetRect(Text.LineHeight);
            Widgets.IntEntry(radiusRect, ref Settings.SearchRadiusTiles, ref _searchRadiusBuffer, 1);
            Settings.SearchRadiusTiles = Mathf.Clamp(Settings.SearchRadiusTiles, 1, 250);

            listing.Gap();

            listing.GapLine();
            DrawDrugAddMenu(listing);
            listing.Gap();
            DrawAllowedDrugsSection(listing);

            listing.End();

            DrawActionOrder(rightRect);

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

        private void DrawActionOrder(Rect rect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);

            listing.Label("Action order");

            List<string> order = Settings.ActionOrder;
            if (order == null || order.Count == 0)
            {
                order = new List<string> { "Drug", "Food", "Weapon", "Armor" };
                Settings.ActionOrder = order;
            }

            bool changed = false;
            for (int i = 0; i < order.Count; i++)
            {
                string action = order[i];
                Rect row = listing.GetRect(Text.LineHeight);
                float buttonWidth = 38f;
                float gap = 4f;
                float buttonsWidth = buttonWidth * 2f + gap;

                Rect labelRect = new Rect(row.x, row.y, row.width - buttonsWidth, row.height);
                Rect upRect = new Rect(labelRect.xMax + gap, row.y, buttonWidth, row.height);
                Rect downRect = new Rect(upRect.xMax + gap, row.y, buttonWidth, row.height);

                Widgets.Label(labelRect, action);

                if (Widgets.ButtonText(upRect, "Up") && i > 0)
                {
                    string temp = order[i - 1];
                    order[i - 1] = order[i];
                    order[i] = temp;
                    changed = true;
                }

                if (Widgets.ButtonText(downRect, "Down") && i < order.Count - 1)
                {
                    string temp = order[i + 1];
                    order[i + 1] = order[i];
                    order[i] = temp;
                    changed = true;
                }

                if (changed)
                {
                    break;
                }
            }

            listing.End();
        }
    }
}
