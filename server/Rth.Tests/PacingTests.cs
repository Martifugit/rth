using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.World;
using Xunit.Abstractions;

namespace Rth.Tests;

/// <summary>
/// Fights against the shipped creatures with the starter kit, simulated for an hour of ticks
/// with Revolution: fights should last long enough for phlegm to matter and cost some blood,
/// without being a slog. Prints the numbers, for tuning.
/// </summary>
public class PacingTests
{
    private readonly ITestOutputHelper _out;
    public PacingTests(ITestOutputHelper output) => _out = output;

    [Theory]
    [InlineData("fen_rat", "watchmans_sword")]
    [InlineData("fen_rat", "apprentice_athame")]
    [InlineData("vitriol_mite", "watchmans_sword")]
    [InlineData("vitriol_mite", "apprentice_athame")]
    public void StarterFightsLastLongEnoughToMatter(string npc, string weapon)
    {
        GameContent content = GameContent.Load(Repo.ContentDirectory);
        var fx = new WorldFixture(Maps.Open(40, 40, new TileCoord(5, 5)), new ServerConfig(), content, new[] { new NpcSpawn { Npc = npc, X = 7, Y = 5, Wander = 0 } });
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        ItemSystem items = fx.World.Get<ItemSystem>();
        items.Of(alice).Equipment.Set((int)EquipSlot.Weapon, weapon, 1);
        fx.World.RecalculateStats(alice);
        client.FromClient(new SetClientStateMessage { Key = CombatOptions.RevolutionKey, Json = "all" });
        var style0 = fx.World.Get<CombatSystem>().WeaponStyle(alice);
        var bar = content.Abilities.Values.Where(a => a.Style == style0 && (a.Type == AbilityType.Builder || a.Type == AbilityType.Spender || a.Type == AbilityType.Ultimate))
            .OrderByDescending(a => (int)a.Type).Select(a => "a:" + a.Id).ToArray();
        client.FromClient(new SetClientStateMessage { Key = CombatOptions.BarsKey, Json = Newtonsoft.Json.JsonConvert.SerializeObject(new[] { bar }) });
        fx.Ticks(1);
        CombatSystem combat = fx.World.Get<CombatSystem>();
        int kills = 0, maxPhlegm = 0, taken = 0, lastBlood = alice.Blood, killTicks = 0, fightStart = -1, deaths = 0;
        var phlegmAtKill = new List<int>();
        int ticks = 6000;
        for (int t = 0; t < ticks; t++)
        {
            Npc target = fx.Npc(npc);
            if (!target.IsDead && alice.Interaction == null && !alice.IsDead)
            {
                client.FromClient(new AttackMessage { TargetId = target.Id });
                fightStart = t;
            }
            bool wasDead = target.IsDead;
            fx.Ticks(1);
            if (!wasDead && target.IsDead) { kills++; killTicks += t - fightStart; phlegmAtKill.Add(alice.Phlegm); }
            if (alice.Blood < lastBlood) taken += lastBlood - alice.Blood;
            lastBlood = alice.Blood;
            maxPhlegm = Math.Max(maxPhlegm, alice.Phlegm);
            if (alice.IsDead) deaths++;
            if (alice.Blood < alice.MaxBlood / 3) alice.Blood = alice.MaxBlood; // stands in for eating
        }
        SkillSystem skills = fx.World.Get<SkillSystem>();
        double hours = ticks * 0.6 / 3600;
        string style = combat.WeaponStyle(alice) == CombatStyle.Melee ? "melee" : "occultism";
        _out.WriteLine($"{npc} with {weapon}: kills {kills}, avg fight {(kills == 0 ? 0 : killTicks / (double)kills):0.0} ticks, " +
            $"{style} xp/h {skills.Xp(alice, style) / hours:0}, defence xp/h {skills.Xp(alice, "defence") / hours:0}, level {skills.Level(alice, style)}, " +
            $"max phlegm {maxPhlegm}, avg phlegm at kill {(phlegmAtKill.Count == 0 ? 0 : phlegmAtKill.Average()):0}, blood taken/kill {(kills == 0 ? 0 : taken / kills)}");

        double fight = killTicks / (double)Math.Max(1, kills);
        Assert.True(kills > 20, "kills " + kills);
        Assert.InRange(fight, 5, 50);                        // 3 to 30 seconds a creature
        Assert.True(maxPhlegm >= 50, "phlegm never reached a spender: " + maxPhlegm);
        Assert.InRange(taken / Math.Max(1, kills), 10, alice.MaxBlood / 2); // it hurts, but one fight never nearly kills
    }
}
