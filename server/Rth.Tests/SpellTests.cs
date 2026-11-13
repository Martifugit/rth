using Newtonsoft.Json;
using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>Occultism's spellbooks: known spells, the active one the auto-attack casts, reagents burnt per cast.</summary>
public class SpellTests
{
    private static readonly TileCoord Spawn = new(5, 5);

    private static GameContent Content()
    {
        ContentParts parts = TestContent.Parts();
        parts.Items.Add(new ItemDef { Id = "salt", Name = "Salt", Stackable = true });
        parts.Abilities.Add(new AbilityDef { Id = "salt_bolt", Name = "Salt Bolt", Type = AbilityType.Auto, CooldownTicks = 4, Range = 8, DamageMin = 80, DamageMax = 80 });
        parts.Spellbooks = new SpellbooksFile
        {
            Spellbooks = { new SpellbookDef { Id = "primer", Name = "The Primer" } },
            Spells =
            {
                new SpellDef { Id = "lash", Name = "Lash", Spellbook = "primer", Ability = "zap", Starter = true },
                new SpellDef { Id = "bolt", Name = "Salt Bolt", Spellbook = "primer", Ability = "salt_bolt", Level = 10, Reagents = { new ItemCost { Item = "salt", Count = 1 } } },
            },
        };
        return GameContent.Create(parts);
    }

    private static WorldFixture Arena() =>
        new(Maps.Open(40, 40, Spawn), new ServerConfig(), Content(), new[] { new NpcSpawn { Npc = "dummy", X = 8, Y = 5, Wander = 0 } });

    private static void SetLevel(WorldFixture fx, Player p, int level) =>
        fx.World.Get<SkillSystem>().AddXp(p, "occultism", fx.World.Content.XpCurve.XpForLevel(level));

    [Fact]
    public void ANewCharacterCastsTheStarterSpell()
    {
        var fx = Arena();
        FakeConnection client = fx.Login("Alice");
        SpellSystem spells = fx.World.Get<SpellSystem>();
        Player alice = fx.Player("Alice");
        Assert.Equal("lash", spells.Active(alice)?.Id);
        Assert.Equal("zap", fx.World.Get<CombatSystem>().AutoAttack(alice).Id);

        SpellsState state = JsonConvert.DeserializeObject<SpellsState>(client.Sent.OfType<JsonStateMessage>().Last(s => s.Key == SpellsState.Key).Json)!;
        Assert.Equal(new[] { "lash" }, state.Known);
        Assert.Equal("lash", state.Active);
    }

    [Fact]
    public void ALearnedSpellNeedsItsLevelToBeActive()
    {
        var fx = Arena();
        FakeConnection client = fx.Login("Alice");
        SpellSystem spells = fx.World.Get<SpellSystem>();
        Player alice = fx.Player("Alice");
        Assert.True(spells.Learn(alice, "bolt"));
        Assert.False(spells.Learn(alice, "bolt")); // once

        client.FromClient(new IntentMessage { Kind = SpellsState.Intent, Arg = "bolt" });
        fx.Ticks(1);
        Assert.Contains("Salt Bolt needs Occultism 10.", client.Texts);
        Assert.Equal("lash", spells.Active(alice)?.Id);

        SetLevel(fx, alice, 10);
        client.FromClient(new IntentMessage { Kind = SpellsState.Intent, Arg = "bolt" });
        fx.Ticks(1);
        Assert.Equal("bolt", spells.Active(alice)?.Id);
        Assert.Contains("Your occult attacks now cast Salt Bolt.", client.Texts);
    }

    [Fact]
    public void EachCastBurnsReagentsAndRunningOutStopsTheFight()
    {
        var fx = Arena();
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        SpellSystem spells = fx.World.Get<SpellSystem>();
        ItemSystem items = fx.World.Get<ItemSystem>();
        SetLevel(fx, alice, 10);
        spells.Learn(alice, "bolt");
        spells.SetActive(alice, "bolt");
        items.Give(alice, "salt", 2);
        Npc dummy = fx.Npc("dummy");

        client.FromClient(new AttackMessage { TargetId = dummy.Id });
        fx.Ticks(1);
        Assert.Equal(dummy.MaxBlood - 80, dummy.Blood); // the spell's damage, not the plain auto-attack's
        Assert.Equal(1, items.CountOf(alice, "salt"));
        fx.Ticks(4);
        Assert.Equal(0, items.CountOf(alice, "salt"));
        fx.Ticks(4);
        Assert.Contains("You're out of salt for Salt Bolt.", client.Texts);
        Assert.Equal(dummy.MaxBlood - 160, dummy.Blood);
        Assert.False(alice.Interaction is AttackInteraction);
    }

    [Fact]
    public void KnownSpellsAndTheActiveOneAreSaved()
    {
        var fx = Arena();
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        SpellSystem spells = fx.World.Get<SpellSystem>();
        SetLevel(fx, alice, 10);
        spells.Learn(alice, "bolt");
        spells.SetActive(alice, "bolt");
        client.FromClient(new LogoutMessage());
        fx.Ticks(2);

        fx.Login("Alice");
        Player again = fx.Player("Alice");
        Assert.True(spells.Knows(again, "bolt"));
        Assert.Equal("bolt", spells.Active(again)?.Id);
    }

    [Fact]
    public void AMeleeWeaponIgnoresTheSpell()
    {
        var fx = Arena();
        fx.Login("Alice");
        Player alice = fx.Player("Alice");
        // Empty hands fight occult; the spell is the auto-attack only for occult.
        Assert.Equal(CombatStyle.Occult, fx.World.Get<CombatSystem>().WeaponStyle(alice));
        Assert.Equal("zap", fx.World.Get<CombatSystem>().AutoAttack(alice).Id);
    }

    [Fact]
    public void ASpellMustCastAnOccultAutoAttack()
    {
        ContentParts parts = TestContent.Parts();
        parts.Spellbooks = new SpellbooksFile
        {
            Spellbooks = { new SpellbookDef { Id = "primer", Name = "The Primer" } },
            Spells = { new SpellDef { Id = "odd", Name = "Odd", Spellbook = "primer", Ability = "spark" } },
        };
        var e = Assert.Throws<ContentException>(() => GameContent.Create(parts));
        Assert.Contains("must be an occult auto-attack", e.Message);
    }
}
