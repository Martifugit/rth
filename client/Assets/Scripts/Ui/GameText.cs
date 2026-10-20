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
            StatIds.MaxBreath => "Max breath",
            _ => stat,
        };

        public static string StatUnit(string stat) =>
            stat == StatIds.Accuracy || stat == StatIds.DamageBonus || stat == StatIds.Ward ? "%" : "";

        /// <summary>A tooltip card for an item (rich text).</summary>
        public static string Describe(ItemDef def, int count)
        {
            var lines = new List<string> { UiKit.Title(def.Name) + (count > 1 ? UiKit.Colour("  x" + count.ToString("N0"), UiKit.Value) : "") };
            string kind = def.Equipment != null ? def.Equipment.Slot.ToString() : def.Consumable != null ? def.Consumable.Verb == "Drink" ? "Drink" : "Food" : def.Stackable ? "Stackable" : "";
            if (kind.Length > 0) lines.Add(UiKit.Colour(kind, UiKit.Muted));
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
            "restoreBreath" => "Restores " + op.Amount + " breath",
            "addPhlegm" => "+" + op.Amount + " phlegm",
            "shiftBile" => "Bile " + (op.Amount > 0 ? "+" : "") + op.Amount,
            _ => op.Op + " " + op.Amount,
        };

        /// <summary>A tooltip card for an ability (rich text): what it does, what it costs, what it needs.</summary>
        public static string Describe(AbilityDef a, float tickSeconds, string problem = null)
        {
            var lines = new List<string>
            {
                UiKit.Title(a.Name),
                UiKit.Colour(a.Type + " ability, " + a.Style.ToString().ToLowerInvariant(), UiKit.Muted),
                a.Description,
                "",
                "Damage: " + UiKit.Colour(a.DamageMin + "-" + a.DamageMax + "%", UiKit.Value) + "   Range: " + UiKit.Colour(a.Range.ToString(), UiKit.Value),
                "Cooldown: " + UiKit.Colour((a.CooldownTicks * tickSeconds).ToString("0.#") + "s", UiKit.Value),
            };
            if (a.PhlegmGain > 0) lines.Add("Builds " + UiKit.Colour(a.PhlegmGain + " phlegm", UiKit.Good));
            if (a.PhlegmRequired > 0) lines.Add("Needs " + UiKit.Colour(a.PhlegmRequired + "% phlegm", UiKit.Value) + ", spends " + a.PhlegmCost);
            if (a.BileShift != 0) lines.Add("Bile " + UiKit.Colour((a.BileShift > 0 ? "+" : "") + a.BileShift, a.BileShift > 0 ? new Color(0.95f, 0.8f, 0.2f) : new Color(0.72f, 0.55f, 0.95f)));
            if (a.BileTowardZero > 0) lines.Add("Bile " + a.BileTowardZero + " towards balance");
            if (a.BileMin > -100) lines.Add("Needs bile of at least " + (a.BileMin > 0 ? "+" : "") + a.BileMin);
            if (a.BileMax < 100) lines.Add("Needs bile of at most " + a.BileMax);
            foreach (Rth.Shared.Skills.SkillRequirement r in a.Requirements) lines.Add("Needs " + r.Skill + " level " + r.Level);
            if (problem != null) lines.Add("\n" + UiKit.Colour(problem, UiKit.Bad));
            return string.Join("\n", lines);
        }

        /// <summary>The ability's colour: yellow bile, black bile, or neutral.</summary>
        public static Color AbilityColor(AbilityDef ability) =>
            ability.BileShift > 0 ? new Color(0.62f, 0.5f, 0.12f)
            : ability.BileShift < 0 ? new Color(0.26f, 0.17f, 0.34f)
            : new Color(0.3f, 0.36f, 0.3f);

        /// <summary>What clicking an item does: deposit with the bank open, sell in a shop, otherwise its first action.</summary>
        public static void DefaultAction(GameSession session, ContainerKind where, int slot, ItemDef def)
        {
            if (def == null) return;
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
