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

        public static string Describe(ItemDef def, int count)
        {
            var lines = new List<string> { def.Name + (count > 1 ? " x" + count : "") };
            if (!string.IsNullOrEmpty(def.Examine)) lines.Add(def.Examine);
            if (def.Equipment != null)
            {
                var stats = new List<string>();
                foreach (KeyValuePair<string, int> s in def.Equipment.Stats)
                    stats.Add((s.Value > 0 ? "+" : "") + s.Value + StatUnit(s.Key) + " " + StatLabel(s.Key).ToLowerInvariant());
                lines.Add(def.Equipment.Slot + ": " + string.Join(", ", stats));
            }
            if (def.Consumable != null)
            {
                var effects = new List<string>();
                foreach (EffectOp op in def.Consumable.Effects) effects.Add(op.Op + " " + op.Amount);
                lines.Add(def.Consumable.Verb + ": " + string.Join(", ", effects));
            }
            return string.Join("\n", lines);
        }

        public static string Describe(AbilityDef a, float tickSeconds)
        {
            var lines = new List<string> { a.Name + "  (" + a.Type + ")", a.Description };
            var costs = new List<string>();
            if (a.PhlegmGain > 0) costs.Add("+" + a.PhlegmGain + " phlegm");
            if (a.PhlegmRequired > 0) costs.Add("needs " + a.PhlegmRequired + "% phlegm, spends " + a.PhlegmCost);
            if (a.BileShift != 0) costs.Add("bile " + (a.BileShift > 0 ? "+" : "") + a.BileShift);
            if (a.BileTowardZero > 0) costs.Add("bile " + a.BileTowardZero + " towards balance");
            if (a.BileMin > -100) costs.Add("bile at least " + (a.BileMin > 0 ? "+" : "") + a.BileMin);
            if (a.BileMax < 100) costs.Add("bile at most " + a.BileMax);
            costs.Add(a.DamageMin + "-" + a.DamageMax + "% damage");
            costs.Add("range " + a.Range + ", cooldown " + (a.CooldownTicks * tickSeconds).ToString("0.#") + "s");
            foreach (Rth.Shared.Skills.SkillRequirement r in a.Requirements) costs.Add("needs " + r.Skill + " " + r.Level);
            lines.Add(string.Join(" | ", costs));
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
