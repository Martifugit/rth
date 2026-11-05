using System.Collections.Generic;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Net;
using UnityEngine;

namespace Rth.Client.Ui
{
    /// <summary>
    /// What clicking an item or ability does, and how they are described, shared by every panel
    /// that shows them (pack, worn, bank, the ability book, the action bar).
    /// </summary>
    public static class GameText
    {
        public static string StatLabel(string stat) => stat switch
        {
            StatIds.AbilityDamage => "Ability damage",
            StatIds.Accuracy => "Accuracy",
            StatIds.DamageBonus => "Damage bonus",
            StatIds.Ward => "Ward",
            StatIds.MaxBlood => "Max blood",
            StatIds.MaxDevotion => "Max devotion",
            StatIds.DevotionBonus => "Devotion bonus",
            StatIds.Lifesteal => "Life steal",
            StatIds.MeleeAccuracy => "Melee accuracy",
            StatIds.RangedAccuracy => "Ranged accuracy",
            StatIds.OccultAccuracy => "Occult accuracy",
            StatIds.MeleeDamage => "Melee damage",
            StatIds.RangedDamage => "Ranged damage",
            StatIds.OccultDamage => "Occult damage",
            StatIds.DeflectMelee => "Melee deflection",
            StatIds.DeflectRanged => "Ranged deflection",
            StatIds.DeflectOccult => "Occult deflection",
            StatIds.MeleePower => "Melee power",
            StatIds.RangedPower => "Ranged power",
            StatIds.OccultPower => "Occult power",
            StatIds.DeferDamage => "Damage deferred",
            StatIds.BlockHit => "Next hit blocked",
            StatIds.BlockedPhlegm => "Phlegm from a block",
            StatIds.ScarOnHit => "Scars per wound",
            StatIds.BuilderPhlegm => "Builder phlegm",
            StatIds.BloodPerAbility => "Blood per ability",
            _ => stat,
        };

        public static string StatUnit(string stat) =>
            stat == StatIds.AbilityDamage || stat == StatIds.MaxBlood || stat == StatIds.MaxDevotion || stat == StatIds.MeleePower
            || stat == StatIds.RangedPower || stat == StatIds.OccultPower || stat == StatIds.BlockedPhlegm || stat == StatIds.ScarOnHit
            || stat == StatIds.BuilderPhlegm ? "" : "%";

        /// <summary>A style as players say it: "Occultism", "Melee".</summary>
        /// <summary>"Fen rat (level 4)": a creature's name with its combat level, when it has one.</summary>
        public static string NpcName(GameSession session, string name, NpcInfo info) =>
            info == null || info.Level <= 0 ? name : name + " (level " + info.Level + ")";

        /// <summary>
        /// The colour of a creature's level against the player's own: green when it is weaker,
        /// yellow about even, red when stronger (RuneScape's convention).
        /// </summary>
        public static Color LevelColour(GameSession session, int level)
        {
            int diff = level - session.CombatLevel;
            if (diff <= -4) return UiKit.Good;
            if (diff >= 4) return UiKit.Bad;
            return new Color(0.95f, 0.85f, 0.4f);
        }

        public static string StyleName(CombatStyle style) => style switch
        {
            CombatStyle.Occult => "Occultism",
            // Abilities of no style work with any weapon: Defence's (docs/skills.md 3.6).
            CombatStyle.None => "Defence",
            _ => style.ToString(),
        };

        /// <summary>A tooltip card for an item (rich text).</summary>
        /// <summary>A tackle part's slot and what it sets on the rig: "Tier 3 reel: reel speed 95, run drag 140".</summary>
        public static string TackleLine(Rth.Shared.Angling.AnglingTackleDef t)
        {
            var parts = new List<string>();
            if (t.SnapBuffer > 0) parts.Add("red buffer " + t.SnapBuffer);
            if (t.Strength > 0) parts.Add("strength " + t.Strength);
            if (t.Thickness > 0) parts.Add("thickness " + t.Thickness);
            if (t.ReelSpeed > 0) parts.Add("reel speed " + t.ReelSpeed);
            if (t.RunDrag > 0) parts.Add("run drag " + t.RunDrag);
            if (t.HookHold > 0) parts.Add("hook hold " + t.HookHold);
            if (t.NoTeethWear) parts.Add("teeth don't dull it");
            if (t.Lantern) parts.Add("a lantern");
            if (t.Listening) parts.Add("hears every tell");
            if (t.Bites.Count > 0) parts.Add("takes " + string.Join(", ", t.Bites).Replace("tier:", "tier ") );
            if (t.Collector) parts.Add("only the collector mends it");
            return "Tier " + t.Tier + " " + t.Slot.ToString().ToLowerInvariant() + (parts.Count > 0 ? ": " + string.Join(", ", parts) : "");
        }

        public static string Describe(ItemDef def, int count)
        {
            var lines = new List<string> { UiKit.Title(def.Name) + (count > 1 ? UiKit.Colour("  x" + count.ToString("N0"), UiKit.Value) : "") };
            string kind = def.Equipment != null ? def.Equipment.Slot.ToString() : def.Consumable != null ? def.Consumable.Verb == "Drink" ? "Drink" : "Food" : def.Stackable ? "Stackable" : "";
            if (kind.Length > 0) lines.Add(UiKit.Colour(kind, UiKit.Muted));
            if (def.Tool != null)
                lines.Add(UiKit.Colour("Tier " + def.Tool.Tier + " " + def.Tool.Kind
                    + (def.Tool.Success > 0 ? ", +" + (def.Tool.Success * 100).ToString("0.#") + "% success" : "")
                    + (def.Tool.Conservation > 0 ? ", " + (def.Tool.Conservation * 100).ToString("0.#") + "% conservation" : ""), UiKit.Value));
            if (def.Tackle != null) lines.Add(UiKit.Colour(TackleLine(def.Tackle), UiKit.Value));
            if (!string.IsNullOrEmpty(def.Examine)) lines.Add(def.Examine);
            if (def.Equipment != null && def.Equipment.Stats.Count > 0)
            {
                lines.Add("");
                foreach (KeyValuePair<string, int> s in def.Equipment.Stats)
                    lines.Add(StatLabel(s.Key) + ": " + UiKit.Colour((s.Value > 0 ? "+" : "") + s.Value + StatUnit(s.Key), s.Value >= 0 ? UiKit.Good : UiKit.Bad));
            }
            if (def.Consumable != null && def.Consumable.Effects.Count > 0)
            {
                lines.Add("");
                foreach (EffectOp op in def.Consumable.Effects) lines.Add(UiKit.Colour(EffectLabel(op), UiKit.Good));
            }
            if (def.Value > 0) lines.Add(UiKit.Colour("Worth " + def.Value.ToString("N0") + " marks", UiKit.Muted));
            return string.Join("\n", lines);
        }

        private static string EffectLabel(EffectOp op) => op.Op switch
        {
            "healBlood" => "Heals " + op.Amount + " blood",
            "restoreDevotion" => "Restores " + op.Amount + " devotion",
            "restoreDevotionPercent" => "Restores " + op.Amount + "% of your devotion",
            "addPhlegm" => "+" + op.Amount + " phlegm",
            "shiftBile" => "Bile " + (op.Amount > 0 ? "+" : "") + op.Amount,
            _ => op.Op + " " + op.Amount,
        };

        /// <summary>A tooltip card for an ability (rich text): what it does, what it costs, what it needs.</summary>
        public static string Describe(AbilityDef a, float tickSeconds, string problem = null, System.Func<string, string> itemName = null)
        {
            bool verse = a.Type == AbilityType.Verse;
            var lines = new List<string>
            {
                UiKit.Title(a.Name),
                UiKit.Colour(verse ? "Verse of Cruor, read with a melee weapon" : a.Type + " ability, " + a.Style.ToString().ToLowerInvariant(), UiKit.Muted),
                a.Description,
                "",
            };
            if (a.DamageMax > 0)
                lines.Add("Damage: " + UiKit.Colour(a.DamageMin + "-" + a.DamageMax + "%", UiKit.Value) + "   Range: " + UiKit.Colour(a.Range.ToString(), UiKit.Value));
            lines.Add("Cooldown: " + UiKit.Colour((a.CooldownTicks * tickSeconds).ToString("0.#") + "s", UiKit.Value));
            if (a.Buff != null) lines.Add("Lasts " + UiKit.Colour((a.Buff.Ticks * tickSeconds).ToString("0.#") + "s", UiKit.Value));
            if (a.BloodCostCurrent > 0) lines.Add("Costs " + UiKit.Colour(a.BloodCostCurrent + "% of your blood", UiKit.Bad));
            if (a.BloodCostMax > 0) lines.Add("Costs " + UiKit.Colour(a.BloodCostMax + "% of your max blood", UiKit.Bad));
            foreach (ItemCost r in a.Reagents) lines.Add((verse ? "Reads " : "Uses ") + UiKit.Colour(r.Count + " " + (itemName?.Invoke(r.Item) ?? r.Item).ToLowerInvariant(), UiKit.Value));
            if (a.BleedPercent > 0) lines.Add("Bleeds " + UiKit.Colour(a.BleedPercent + "% of the hit", UiKit.Value) + " over " + (a.BleedTicks * tickSeconds).ToString("0.#") + "s");
            if (a.LowBloodBonus > 0) lines.Add("Up to " + UiKit.Colour(a.LowBloodBonus + "% more damage", UiKit.Value) + " the lower your blood");
            if (a.PhlegmGain > 0) lines.Add("Builds " + UiKit.Colour(a.PhlegmGain + " phlegm", UiKit.Good));
            if (a.PhlegmRequired > 0) lines.Add("Needs " + UiKit.Colour(a.PhlegmRequired + "% phlegm", UiKit.Value) + ", spends " + a.PhlegmCost);
            if (a.BileShift != 0) lines.Add("Bile " + UiKit.Colour((a.BileShift > 0 ? "+" : "") + a.BileShift, a.BileShift > 0 ? new Color(0.95f, 0.8f, 0.2f) : new Color(0.72f, 0.55f, 0.95f)));
            if (a.BileTowardZero > 0) lines.Add("Bile " + a.BileTowardZero + " towards balance");
            if (a.BileMin > -100) lines.Add("Needs bile of at least " + (a.BileMin > 0 ? "+" : "") + a.BileMin);
            if (a.BileMax < 100) lines.Add("Needs bile of at most " + a.BileMax);
            if (a.OnSelf) lines.Add(UiKit.Colour("Used on yourself; keeps you in combat without breaking a stalled ability", UiKit.Good));
            foreach (Rth.Shared.Skills.SkillRequirement r in a.Requirements) lines.Add("Needs " + r.Skill + " level " + r.Level);
            if (problem != null) lines.Add("\n" + UiKit.Colour(problem, UiKit.Bad));
            return string.Join("\n", lines);
        }

        /// <summary>The ability's colour: blood for melee and verses, else yellow bile, black bile, or neutral.</summary>
        public static Color AbilityColor(AbilityDef ability) =>
            ability.Style == CombatStyle.Melee ? new Color(0.5f, 0.14f, 0.16f)
            : ability.BileShift > 0 ? new Color(0.62f, 0.5f, 0.12f)
            : ability.BileShift < 0 ? new Color(0.26f, 0.17f, 0.34f)
            : new Color(0.3f, 0.36f, 0.3f);

        /// <summary>What clicking an item does: deposit with the bank open, sell in a shop, otherwise its first action.</summary>
        public static void DefaultAction(GameSession session, ContainerKind where, int slot, ItemDef def)
        {
            if (def == null) return;
            // An item picked up with "Use" is used on whatever pack item is clicked next.
            if (session.IsUsingItem)
            {
                if (where == ContainerKind.Inventory) session.UseOnItem(slot);
                else session.CancelUse();
                return;
            }
            if (where == ContainerKind.Inventory && session.OpenInterface == InterfaceKind.Bank)
            {
                session.BankDeposit(slot, 1);
                return;
            }
            if (where == ContainerKind.Inventory && session.OpenInterface == InterfaceKind.Shop)
            {
                session.ShopSell(slot, 1);
                return;
            }
            List<ItemAction> actions = ItemActions.For(def, where);
            if (actions.Count > 0) session.UseItem(where, slot, actions[0].Id);
        }

        /// <summary>Every option for an item, for a right-click menu.</summary>
        public static List<(string Label, System.Action Action)> ItemOptions(GameSession session, ContainerKind where, int slot, ItemDef def)
        {
            var options = new List<(string Label, System.Action Action)>();
            if (session.IsUsingItem)
            {
                if (where == ContainerKind.Inventory && slot != session.UseSlot)
                    options.Add((session.UseLabel + " -> " + def.Name, () => session.UseOnItem(slot)));
                options.Add(("Cancel", () => session.CancelUse()));
                return options;
            }
            if (where == ContainerKind.Inventory && session.OpenInterface == InterfaceKind.Bank)
            {
                options.Add(("Deposit 1", () => session.BankDeposit(slot, 1)));
                options.Add(("Deposit 5", () => session.BankDeposit(slot, 5)));
                options.Add(("Deposit all", () => session.BankDeposit(slot, 0)));
            }
            else if (where == ContainerKind.Inventory && session.OpenInterface == InterfaceKind.Shop && session.Shop != null && session.Shop.SellPercent > 0)
            {
                int each = def.Value * session.Shop.SellPercent / 100;
                options.Add(("Sell 1 (" + each + " each)", () => session.ShopSell(slot, 1)));
                options.Add(("Sell 5", () => session.ShopSell(slot, 5)));
                options.Add(("Sell all", () => session.ShopSell(slot, 1000)));
            }
            foreach (ItemAction a in ItemActions.For(def, where))
            {
                string id = a.Id;
                options.Add((a.Label + " " + def.Name, () => session.UseItem(where, slot, id)));
            }
            return options;
        }
    }
}
