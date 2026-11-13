using System;
using System.Linq;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Net;

namespace Rth.Server.Game
{
    /// <summary>
    /// Tipped ammunition (tips.json): a shot that lands may set off the tip of the arrow in the
    /// quiver, more often the steadier the aim. What it does is the tip's data.
    /// </summary>
    public sealed partial class CombatSystem
    {
        /// <summary>The tip on the ammunition in a player's quiver, or null.</summary>
        public TipDef? QuiverTip(Player player)
        {
            string? tip = World.Find<ItemSystem>()?.Worn(player, EquipSlot.Ammo)?.Ammo?.Tip;
            return tip != null && World.Content.Tips.TryGetValue(tip, out TipDef? def) ? def : null;
        }

        /// <summary>The percent chance a landed shot sets the tip off at this calibration.</summary>
        public static double TipChance(TipDef tip, int calibration) =>
            tip.Chance + tip.ChanceCalibrated * Math.Max(0, Math.Min(100, calibration)) / 100.0;

        /// <summary>Rolls the quiver's tip after a shot of <paramref name="dealt"/> damage landed.</summary>
        private void SetOffTip(Player player, Entity target, int dealt, int calibration)
        {
            if (dealt <= 0) return;
            TipDef? tip = QuiverTip(player);
            if (tip == null || World.Random.NextDouble() * 100 >= TipChance(tip, calibration)) return;

            TipEffect e = tip.Effect;
            if (e.Message.Length > 0) World.Tell(player, TextKind.Info, e.Message);
            if (e.DamagePercent > 0 && !target.IsDead) Damage(target, Math.Max(1, dealt * e.DamagePercent / 100), player, HitKind.Normal, CombatStyle.Ranged);
            if (e.BleedPercent > 0 && !target.IsDead) AddBleed(target, Math.Max(1, dealt * e.BleedPercent / 100), e.BleedTicks, player, CombatStyle.Ranged);
            if (e.PinTicks > 0 && !target.IsDead)
            {
                target.RootedUntilTick = Math.Max(target.RootedUntilTick, World.CurrentTick + (uint)e.PinTicks);
                target.Waypoints.Clear();
            }
            if (e.HealPercent > 0) Heal(player, Math.Max(1, dealt * e.HealPercent / 100));
            if (e.SplashRadius > 0)
            {
                int splash = Math.Max(1, dealt * e.SplashPercent / 100);
                foreach (Npc other in World.EntitiesNear(target.Plane, target.Position, e.SplashRadius).OfType<Npc>().ToList())
                    if (other != target && !other.IsDead && other.Def.Attackable && other.Position.ChebyshevDistance(target.Position) <= e.SplashRadius)
                        Damage(other, splash, player, HitKind.Normal, CombatStyle.Ranged);
            }
        }
    }
}
