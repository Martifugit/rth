using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.World;
using Xunit.Abstractions;

namespace Rth.Tests;

/// <summary>
/// The shipped long way back (world/regions/realm_long_way.json, trials.json), walked by a bot:
/// it knows the route, warms its hands at each lantern, steps round the hands it knows of and waits
/// for shades to pass. Done well it should take about three minutes and arrive with a little blood
/// left; skipping the lanterns, or dawdling, should kill it. Prints the numbers for tuning.
/// </summary>
public class LongWayPaceTests
{
    private readonly ITestOutputHelper _out;

    public LongWayPaceTests(ITestOutputHelper output) => _out = output;

    private static readonly string Maps = Path.Combine(Repo.ServerDirectory, "data", "maps");
    private static readonly (int dx, int dy)[] Steps = { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1) };

    private sealed class Outcome
    {
        public bool Won;
        public bool Died;
        public int Ticks;
        public int LowestBlood = int.MaxValue;
        public int ShadeHits;
        public int Grabs;
        public int Lanterns;
    }

    private static WorldFixture Fixture()
    {
        var world = new WorldMap();
        var spawns = new List<NpcSpawn>();
        var objects = new List<PlacedObject>();
        foreach (string name in new[] { "realm_of_the_dead", "realm_long_way" })
        {
            string path = Path.Combine(Maps, name + ".rthmap");
            MapData map = MapData.Load(path);
            world.AddRegion(new WorldRegion { Name = name, Scene = name, X = map.OriginX, Y = map.OriginY, Width = map.Width, Height = map.Height }, map);
            MapPlacements placed = MapPlacements.Load(path);
            spawns.AddRange(placed.Npcs);
            objects.AddRange(placed.Objects);
        }
        GameContent content = GameContent.Load(Repo.ContentDirectory);
        world.Spawn = new TileCoord(content.Death.Wake!.X, content.Death.Wake.Y);
        return new WorldFixture(world, new Rth.Server.ServerConfig { RandomSeed = 7 }, content, spawns, objects);
    }

    /// <summary>Shortest walk inside the long way's region, keeping off the tiles in <paramref name="avoid"/> where it can.</summary>
    private static List<TileCoord>? Route(WorldFixture fx, TileCoord from, Func<TileCoord, bool> goal, HashSet<TileCoord> avoid)
    {
        WorldRegion region = ((WorldMap)fx.World.Map).Regions.First(r => r.Name == "realm_long_way");
        var came = new Dictionary<TileCoord, TileCoord> { [from] = from };
        var queue = new Queue<TileCoord>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            TileCoord at = queue.Dequeue();
            if (goal(at))
            {
                var path = new List<TileCoord>();
                for (TileCoord t = at; t != from; t = came[t]) path.Add(t);
                path.Reverse();
                return path;
            }
            foreach ((int dx, int dy) in Steps)
            {
                var next = at.Offset(dx, dy);
                if (!region.Contains(next.X, next.Y) || came.ContainsKey(next) || avoid.Contains(next)) continue;
                if (!fx.World.Map.CanStep(0, at.X, at.Y, dx, dy)) continue;
                came[next] = at;
                queue.Enqueue(next);
            }
        }
        return null;
    }

    private Outcome Walk(bool lanterns, bool careful, int dawdleTicks = 0)
    {
        WorldFixture fx = Fixture();
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        ItemSystem items = fx.World.Get<ItemSystem>();
        items.Of(alice).Equipment.Set((int)EquipSlot.Weapon, "watchmans_sword", 1);
        fx.World.Get<CombatSystem>().Damage(alice, 1_000_000, null, HitKind.Normal);
        fx.Ticks(fx.World.Rules.RespawnDelayTicks + 2);
        fx.World.Get<DialogueSystem>().End(alice);
        TrialSystem trials = fx.World.Get<TrialSystem>();
        // Out along the Landing's causeway, under the stones and over the threshold: that begins it.
        fx.World.Get<DeathSystem>().Move(alice, 0, new TileCoord(8208, 8194));
        fx.Ticks(1);
        client.FromClient(new WalkToMessage { X = 8208, Y = 8189 });
        fx.Ticks(8);
        Assert.True(trials.Running(alice, "long_way"), "didn't begin: " + string.Join(" / ", client.Texts.TakeLast(3)) + " at " + alice.Position);

        var outcome = new Outcome();
        fx.World.Events.Subscribe<DamageDealt>(e => { if (e.Target == alice && e.Source is Npc) outcome.ShadeHits++; });
        fx.World.Events.Subscribe<TriggerSprung>(e => { if (e.Player == alice) outcome.Grabs++; });
        fx.World.Events.Subscribe<EntityDied>(e => { if (e.Victim == alice) outcome.Died = true; });

        var hands = new HashSet<TileCoord>(fx.World.Get<ObjectSystem>().Placed.Where(o => o.Type == "grasping_hands").Select(o => o.Tile));
        HashSet<TileCoord> avoid = careful ? hands : new HashSet<TileCoord>();
        var stops = new List<(PlacedObject? Lantern, Func<TileCoord, bool> Beside)>();
        TileCoord here = alice.Position;
        if (lanterns)
        {
            // In the order the route meets them.
            var all = fx.World.Get<ObjectSystem>().Placed.Where(o => o.Type == "soul_lantern").ToList();
            foreach (PlacedObject l in all.OrderBy(l => Route(fx, here, t => t.ChebyshevDistance(l.Tile) == 1, avoid)?.Count ?? int.MaxValue))
                stops.Add((l, t => fx.World.IsBeside(new Probe(t), 0, l.Tile)));
        }
        Npc walker = fx.Npc("walker");
        stops.Add((null, t => t.ChebyshevDistance(walker.Position) == 1));

        uint start = fx.World.CurrentTick;
        fx.Ticks(dawdleTicks);
        foreach ((PlacedObject? lantern, Func<TileCoord, bool> beside) in stops)
        {
            List<TileCoord>? path = Route(fx, alice.Position, beside, avoid) ?? Route(fx, alice.Position, beside, new HashSet<TileCoord>());
            Assert.NotNull(path);
            int i = 0;
            while (i < path!.Count && !alice.IsDead && fx.World.CurrentTick - start < 900)
            {
                TileCoord next = path[i];
                bool shadeAhead = careful && fx.World.Npcs.Any(n => n.Def.Id == "shade" && !n.IsDead && n.TargetId == 0 &&
                    n.Position.ChebyshevDistance(next) <= 3 && n.Position.ChebyshevDistance(alice.Position) > 2);
                if (!shadeAhead && alice.RootedUntilTick <= fx.World.CurrentTick)
                {
                    alice.Waypoints.Clear();
                    alice.Waypoints.Enqueue(next);
                }
                fx.Ticks(1);
                if (alice.Position == next) i++;
                outcome.LowestBlood = Math.Min(outcome.LowestBlood, alice.Blood);
            }
            if (alice.IsDead || outcome.Died) break;
            if (lantern != null)
            {
                client.FromClient(new ObjectActionMessage { ObjectType = lantern.Type, X = lantern.X, Y = lantern.Y, ActionId = "kindle" });
                fx.Ticks(2);
                outcome.Lanterns++;
            }
        }
        if (!outcome.Died && !alice.IsDead)
        {
            fx.World.Get<DialogueSystem>().Start(alice, "walker", "The walker");
            fx.Ticks(1);
            for (int step = 0; step < 4; step++)
            {
                DialogueMessage line = client.Sent.OfType<DialogueMessage>().Last();
                if (!line.Open) break;
                client.FromClient(new DialogueChoiceMessage { Option = Math.Max(0, line.Options.IndexOf("Thank you.")) });
                fx.Ticks(1);
            }
            outcome.Won = items.Of(alice).Equipment[(int)EquipSlot.Weapon].ItemId == "watchmans_sword";
        }
        outcome.Ticks = (int)(fx.World.CurrentTick - start);
        _out.WriteLine($"lanterns={lanterns} careful={careful} dawdle={dawdleTicks}: won={outcome.Won} died={outcome.Died} ticks={outcome.Ticks} ({outcome.Ticks * 0.6:0}s) lowest blood={outcome.LowestBlood}/{alice.MaxBlood} shade hits={outcome.ShadeHits} grabs={outcome.Grabs} lanterns used={outcome.Lanterns}");
        return outcome;
    }

    /// <summary>A stand-in entity for asking <see cref="World.IsBeside"/> about a tile.</summary>
    private sealed class Probe : Entity
    {
        public Probe(TileCoord at) : base(-1, "probe") { Position = at; Plane = 0; }
        public override EntityKind Kind => EntityKind.Npc;
        public override EntityAdded Describe() => new EntityAdded();
    }

    [Fact]
    public void ACarefulWalkerMakesItInAboutThreeMinutes()
    {
        Outcome o = Walk(lanterns: true, careful: true);
        Assert.True(o.Won, "the careful walk didn't make it");
        Assert.InRange(o.Ticks, 220, 380);
        Assert.True(o.LowestBlood > 0);
    }

    [Fact]
    public void WithoutTheLanternsTheColdWins()
    {
        Outcome o = Walk(lanterns: false, careful: true);
        Assert.False(o.Won);
        Assert.True(o.Died);
    }

    [Fact]
    public void DawdlingAtTheStartIsFatal()
    {
        Outcome o = Walk(lanterns: true, careful: true, dawdleTicks: 120);
        Assert.False(o.Won);
    }
}
