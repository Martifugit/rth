using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Server.Persistence;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>Death v1: the ferryman's hold, his fee, the obol, expiry, saving, waking and going back.</summary>
public class DeathTests
{
    private static readonly TileCoord Spawn = new(5, 5);
    private static readonly TileCoord Realm = new(15, 15);

    /// <summary>Real time the tests can move.</summary>
    private sealed class Clock
    {
        public DateTime Now = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
    }

    /// <summary>
    /// A 20x20 field: the spawn at (5,5) and the realm of the dead at (15,15). Swords are worth
    /// 1000 and hats 200, so a sword and a hat cost 120 coins to bring back (10%, free under 100).
    /// </summary>
    private static (WorldFixture Fx, Clock Clock) Field(Action<ContentParts>? tweak = null)
    {
        ContentParts parts = TestContent.Parts();
        parts.Items.First(i => i.Id == "sword").Value = 1000;
        parts.Items.First(i => i.Id == "hat").Value = 200;
        parts.Items.First(i => i.Id == "gem").Value = 30;
        parts.Death.Wake = new TilePlace { Plane = 0, X = Realm.X, Y = Realm.Y };
        tweak?.Invoke(parts);
        var clock = new Clock();
        var config = new Rth.Server.ServerConfig { UtcNow = () => clock.Now };
        var fx = new WorldFixture(Maps.Open(20, 20, Spawn), config, GameContent.Create(parts), new[] { new NpcSpawn { Npc = "dummy", X = 1, Y = 1 } });
        return (fx, clock);
    }

    private static ItemSystem Items(WorldFixture fx) => fx.World.Get<ItemSystem>();
    private static FerrySystem Ferry(WorldFixture fx) => fx.World.Get<FerrySystem>();
    private static ItemContainerView Pack(WorldFixture fx, string name) => new(Items(fx).Of(fx.Player(name)).Inventory);
    private static ItemContainerView Worn(WorldFixture fx, string name) => new(Items(fx).Of(fx.Player(name)).Equipment);

    private readonly struct ItemContainerView
    {
        private readonly Rth.Server.Items.ItemContainer _c;
        public ItemContainerView(Rth.Server.Items.ItemContainer c) => _c = c;
        public int Count(string id) => _c.CountOf(id);
        public bool Empty => _c.FreeSlots == _c.Size;
    }

    /// <summary>Gives Alice a worn sword and hat, and whatever else is asked for in her pack.</summary>
    private static void Kit(WorldFixture fx, params (string Item, int Count)[] pack)
    {
        Player alice = fx.Player("Alice");
        PlayerItems items = Items(fx).Of(alice);
        items.Equipment.Set((int)EquipSlot.Weapon, "sword", 1);
        items.Equipment.Set((int)EquipSlot.Head, "hat", 1);
        foreach ((string item, int count) in pack) Assert.Equal(0, Items(fx).Give(alice, item, count));
        fx.World.RecalculateStats(alice);
    }

    private static void Kill(WorldFixture fx, string name = "Alice") =>
        fx.World.Get<CombatSystem>().Damage(fx.Player(name), 1_000_000, fx.Npc("dummy"), HitKind.Normal);

    /// <summary>Dies, and waits until awake in the realm.</summary>
    private static void DieAndWake(WorldFixture fx)
    {
        Kill(fx);
        fx.Ticks(fx.World.Rules.RespawnDelayTicks + 1);
        Assert.False(fx.Player("Alice").IsDead);
    }

    private static void Choose(FakeConnection client, WorldFixture fx, string option)
    {
        DialogueMessage line = client.Sent.OfType<DialogueMessage>().Last(m => m.Open);
        int index = line.Options.IndexOf(option);
        Assert.True(index >= 0, "no option '" + option + "' in: " + string.Join(", ", line.Options));
        client.FromClient(new DialogueChoiceMessage { Option = index });
        fx.Ticks(1);
    }

    // ------------------------------------------------------------------ dying

    [Fact]
    public void EverythingCarriedAndWornGoesToTheFerryman()
    {
        var (fx, _) = Field();
        FakeConnection client = fx.Login("Alice");
        Kit(fx, ("bread", 3), ("coins", 40));
        Kill(fx);

        Assert.True(Pack(fx, "Alice").Empty);
        Assert.True(Worn(fx, "Alice").Empty);
        FerryHold hold = Ferry(fx).Of(fx.Player("Alice"));
        Assert.Equal(1, hold.Items.Single(h => h.Item == "sword").Count);
        Assert.Equal(EquipSlot.Weapon, hold.Items.Single(h => h.Item == "sword").Worn);
        Assert.Equal(3, hold.Items.Where(h => h.Item == "bread").Sum(h => h.Count));
        Assert.Equal(40, hold.Items.Single(h => h.Item == "coins").Count);
        Assert.True(hold.InRealm);
        Assert.Contains(client.Sent.OfType<GameTextMessage>(), t => t.Kind == TextKind.Ferryman && t.Text == "first Alice");
    }

    [Fact]
    public void TheDeadWakeInTheRealmAndTheFerrymanSpeaks()
    {
        var (fx, _) = Field();
        FakeConnection client = fx.Login("Alice");
        Kit(fx);
        DieAndWake(fx);

        Assert.Equal(Realm, fx.Player("Alice").Position);
        DialogueMessage line = client.Sent.OfType<DialogueMessage>().Last();
        Assert.Equal("Ferryman", line.Speaker);
        Assert.Equal("Fee 120 coins for 1,200 coins, kept 24 hours.", line.Text);
    }

    [Fact]
    public void TheShippedFerrymanExplainsHimselfOnlyTheFirstTime()
    {
        DialogueDef shipped = GameContent.Load(Repo.ContentDirectory).Dialogues["ferryman"];
        var (fx, _) = Field(parts =>
        {
            parts.Dialogues.RemoveAll(d => d.Id == shipped.Id);
            parts.Dialogues.Add(shipped);
            parts.Death.Dialogue = shipped.Id;
        });
        FakeConnection client = fx.Login("Alice");
        Kit(fx);
        DieAndWake(fx);

        DialogueMessage line = client.Sent.OfType<DialogueMessage>().Last();
        Assert.StartsWith("You died.", line.Text);
        for (int i = 0; i < 10 && line.Options.Count == 0; i++)
        {
            client.FromClient(new DialogueChoiceMessage { Option = 0 });
            fx.Ticks(1);
            line = client.Sent.OfType<DialogueMessage>().Last();
        }
        Assert.Contains("120 coins", line.Text); // past the explanation, on to his fee
        Assert.Contains("Not yet.", line.Options);

        Choose(client, fx, "Not yet.");
        Kill(fx);
        fx.Ticks(fx.World.Rules.RespawnDelayTicks + 1);
        line = client.Sent.OfType<DialogueMessage>().Last();
        Assert.StartsWith("I have your things.", line.Text);
    }

    [Fact]
    public void AWakeTileThatIsntOpenGroundFallsBackToTheSpawn()
    {
        var (fx, _) = Field(p => p.Death.Wake = new TilePlace { X = 500, Y = 500 });
        fx.Login("Alice");
        DieAndWake(fx);
        Assert.Equal(Spawn, fx.Player("Alice").Position);
    }

    // ------------------------------------------------------------------ the fee

    [Fact]
    public void TheFeeIsAShareOfTheValueHeldAndCheapGearGoesFree()
    {
        var (fx, _) = Field();
        fx.Login("Alice");
        Kit(fx);
        Kill(fx);
        Assert.Equal(1200, Ferry(fx).Value(fx.Player("Alice")));
        Assert.Equal(120, Ferry(fx).Fee(fx.Player("Alice")));

        var (cheap, _) = Field();
        FakeConnection client = cheap.Login("Alice");
        cheap.Player("Alice").Deaths = 1; // not the first, so the line is about the fee
        Assert.Equal(0, Items(cheap).Give(cheap.Player("Alice"), "gem", 3)); // 90, under the free line
        Kill(cheap);
        Assert.Equal(0, Ferry(cheap).Fee(cheap.Player("Alice")));
        Assert.Contains("free", client.Texts);
    }

    [Fact]
    public void PayingFromThePurseAndBankReturnsTheGearAndSendsYouBack()
    {
        var (fx, _) = Field();
        FakeConnection client = fx.Login("Alice");
        Kit(fx);
        DieAndWake(fx);
        Player alice = fx.Player("Alice");
        // Coin found after waking: 50 in the purse, the rest in the bank.
        Items(fx).Give(alice, "coins", 50);
        fx.World.Get<BankSystem>().Of(alice).Add(fx.World.Content.Item("coins")!, 100);
        fx.World.Get<DialogueSystem>().Start(alice, "ferry", "Ferryman"); // talk to him again, now able to pay
        Choose(client, fx, "Pay");

        Assert.Equal(Spawn, alice.Position);
        Assert.Equal(0, Pack(fx, "Alice").Count("coins"));
        Assert.Equal(30, fx.World.Get<BankSystem>().Of(alice).CountOf("coins"));
        Assert.Equal(1, Worn(fx, "Alice").Count("sword"));
        Assert.Equal(1, Worn(fx, "Alice").Count("hat"));
        Assert.False(Ferry(fx).Of(alice).Holding);
        Assert.False(Ferry(fx).Of(alice).InRealm);
        Assert.Contains("You pay the ferryman 120 coins.", client.Texts);
    }

    [Fact]
    public void WithoutTheFeeYouGoBackWithoutYourGear()
    {
        var (fx, _) = Field();
        FakeConnection client = fx.Login("Alice");
        Kit(fx);
        DieAndWake(fx);
        Player alice = fx.Player("Alice");

        Assert.False(Ferry(fx).Pay(alice));
        Assert.Contains("You can't afford the ferryman's fee of 120 coins.", client.Texts);
        Assert.Equal(Realm, alice.Position);

        Choose(client, fx, "Leave");
        Assert.Equal(Spawn, alice.Position);
        Assert.True(Worn(fx, "Alice").Empty);
        Assert.True(Ferry(fx).Of(alice).Holding);
        Assert.False(Ferry(fx).Of(alice).InRealm);
    }

    [Fact]
    public void ThePayOptionOnlyShowsWhenYouCanPay()
    {
        var (fx, _) = Field();
        FakeConnection client = fx.Login("Alice");
        Kit(fx);
        DieAndWake(fx);
        Assert.Equal(new[] { "Leave" }, client.Sent.OfType<DialogueMessage>().Last().Options);
    }

    [Fact]
    public void GearReturnsToThePackWhenItsSlotIsTaken()
    {
        var (fx, _) = Field();
        fx.Login("Alice");
        Kit(fx, ("coins", 500));
        DieAndWake(fx);
        Player alice = fx.Player("Alice");
        Items(fx).Of(alice).Equipment.Set((int)EquipSlot.Weapon, "club", 1);
        Items(fx).Give(alice, "coins", 170); // 10% of 1,700: the coins he holds count too

        Assert.True(Ferry(fx).Pay(alice));
        Assert.Equal(1, Worn(fx, "Alice").Count("club"));
        Assert.Equal(1, Pack(fx, "Alice").Count("sword"));
        Assert.Equal(500, Pack(fx, "Alice").Count("coins")); // the 500 he held, after paying 170 from the purse
    }

    // ------------------------------------------------------------------ the obol

    [Fact]
    public void AnObolCarriedAtDeathIsTakenAsTheFee()
    {
        var (fx, _) = Field();
        FakeConnection client = fx.Login("Alice");
        Kit(fx, ("obol", 2));
        DieAndWake(fx);
        Player alice = fx.Player("Alice");

        Assert.True(Ferry(fx).Of(alice).Paid);
        Assert.Equal(0, Ferry(fx).Fee(alice));
        Assert.Contains("obol", client.Texts);
        Assert.Equal(1, Ferry(fx).Of(alice).Items.Where(h => h.Item == "obol").Sum(h => h.Count)); // only one is taken

        Choose(client, fx, "Pay");
        Assert.Equal(Spawn, alice.Position);
        Assert.Equal(1, Worn(fx, "Alice").Count("sword"));
        Assert.Equal(1, Pack(fx, "Alice").Count("obol"));
    }

    [Fact]
    public void AnObolInTheBankDoesNothing()
    {
        var (fx, _) = Field();
        fx.Login("Alice");
        Kit(fx);
        Player alice = fx.Player("Alice");
        fx.World.Get<BankSystem>().Of(alice).Add(fx.World.Content.Item("obol")!, 1);
        Kill(fx);

        Assert.False(Ferry(fx).Of(alice).Paid);
        Assert.Equal(120, Ferry(fx).Fee(alice));
        Assert.Equal(1, fx.World.Get<BankSystem>().Of(alice).CountOf("obol"));
    }

    // ------------------------------------------------------------------ expiry

    [Fact]
    public void UnpaidGearIsGoneAfterADay()
    {
        var (fx, clock) = Field();
        FakeConnection client = fx.Login("Alice");
        Kit(fx);
        DieAndWake(fx);
        Player alice = fx.Player("Alice");
        Ferry(fx).Leave(alice);

        clock.Now = clock.Now.AddHours(23);
        fx.Ticks(100);
        Assert.True(Ferry(fx).Of(alice).Holding);

        clock.Now = clock.Now.AddHours(2);
        fx.Ticks(100);
        Assert.False(Ferry(fx).Of(alice).Holding);
        Assert.Contains("The ferryman no longer holds your belongings. They are gone.", client.Texts);
    }

    [Fact]
    public void DyingAgainAddsToWhatHeHoldsAndRestartsTheClock()
    {
        var (fx, clock) = Field();
        fx.Login("Alice");
        Kit(fx);
        DieAndWake(fx);
        Player alice = fx.Player("Alice");
        Ferry(fx).Leave(alice);

        clock.Now = clock.Now.AddHours(20);
        Items(fx).Give(alice, "gem", 1);
        DieAndWake(fx);
        FerryHold hold = Ferry(fx).Of(alice);
        Assert.Equal(new[] { "gem", "hat", "sword" }, hold.Items.Select(h => h.Item).OrderBy(i => i));
        Assert.Equal(clock.Now.AddHours(24), hold.ExpiresUtc);
        Assert.Equal(123, Ferry(fx).Fee(alice));
    }

    // ------------------------------------------------------------------ saving

    [Fact]
    public void TheHoldIsSavedAndLoaded()
    {
        var (fx, clock) = Field();
        FakeConnection client = fx.Login("Alice");
        Kit(fx);
        DieAndWake(fx);
        client.FromClient(new LogoutMessage());
        fx.Ticks(1);

        FakeConnection again = fx.Login("Alice");
        fx.Ticks(1);
        Player alice = fx.Player("Alice");
        FerryHold hold = Ferry(fx).Of(alice);
        Assert.Equal(new[] { "hat", "sword" }, hold.Items.Select(h => h.Item).OrderBy(i => i));
        Assert.Equal(EquipSlot.Head, hold.Items.Single(h => h.Item == "hat").Worn);
        Assert.Equal(clock.Now.AddHours(24), hold.ExpiresUtc);
        Assert.True(hold.InRealm);
        Assert.Equal(Realm, alice.Position);
        // Logged out in the realm: the ferryman picks up where he left off.
        Assert.Equal("Ferryman", again.Sent.OfType<DialogueMessage>().Last().Speaker);
    }

    [Fact]
    public void LoggingOutWhileDeadSavesYouInTheRealm()
    {
        var (fx, _) = Field();
        FakeConnection client = fx.Login("Alice");
        Kit(fx);
        Kill(fx);
        client.FromClient(new LogoutMessage());
        fx.Ticks(1);
        PlayerSave save = fx.Store.Saves["alice"];
        Assert.Equal((Realm.X, Realm.Y), (save.X, save.Y));
    }

    [Fact]
    public void GearThatExpiredWhileLoggedOutIsGoneAtLogin()
    {
        var (fx, clock) = Field();
        FakeConnection client = fx.Login("Alice");
        Kit(fx);
        DieAndWake(fx);
        Ferry(fx).Leave(fx.Player("Alice"));
        client.FromClient(new LogoutMessage());
        fx.Ticks(1);

        clock.Now = clock.Now.AddDays(2);
        FakeConnection again = fx.Login("Alice");
        Assert.False(Ferry(fx).Of(fx.Player("Alice")).Holding);
        Assert.Contains("The ferryman no longer holds your belongings. They are gone.", again.Texts);
    }
}
