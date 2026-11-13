using Newtonsoft.Json;
using Rth.Server.Game;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>Friends, ignores, private messages and groups, all typed in chat.</summary>
public class SocialTests
{
    private static WorldFixture World() => new(Maps.Open(40, 40, new TileCoord(5, 5)));

    private static void Say(WorldFixture fx, FakeConnection c, string text)
    {
        c.FromClient(new ChatMessage { Channel = ChatChannel.Public, Text = text });
        fx.Ticks(1);
    }

    private static List<ChatLineMessage> Lines(FakeConnection c, ChatChannel channel) =>
        c.Sent.OfType<ChatLineMessage>().Where(l => l.Channel == channel).ToList();

    private static SocialState Social(FakeConnection c) =>
        JsonConvert.DeserializeObject<SocialState>(c.Sent.OfType<JsonStateMessage>().Last(s => s.Key == SocialState.Key).Json)!;

    [Fact]
    public void APrivateMessageReachesOnlyItsRecipientAndCanBeAnswered()
    {
        var fx = World();
        FakeConnection alice = fx.Login("Alice");
        FakeConnection bob = fx.Login("Bob");
        FakeConnection carol = fx.Login("Carol");

        Say(fx, alice, "/w bob meet me at the mill");
        ChatLineMessage got = Assert.Single(Lines(bob, ChatChannel.PrivateIn));
        Assert.Equal("From Alice", got.From);
        Assert.Equal("meet me at the mill", got.Text);
        Assert.Equal("To Bob", Assert.Single(Lines(alice, ChatChannel.PrivateOut)).From);
        Assert.Empty(Lines(carol, ChatChannel.PrivateIn));
        Assert.DoesNotContain(carol.Sent.OfType<ChatLineMessage>(), l => l.Text.Contains("mill")); // not said out loud

        Say(fx, bob, "/r on my way");
        Assert.Equal("on my way", Assert.Single(Lines(alice, ChatChannel.PrivateIn)).Text);
    }

    [Fact]
    public void NamesWithSpacesWork()
    {
        var fx = World();
        FakeConnection alice = fx.Login("Alice");
        FakeConnection old = fx.Login("Old Tom");
        Say(fx, alice, "/w old tom hello there");
        Assert.Equal("hello there", Assert.Single(Lines(old, ChatChannel.PrivateIn)).Text);
    }

    [Fact]
    public void FriendsAreToldWhenYouComeAndGoAndTheListIsSaved()
    {
        var fx = World();
        FakeConnection alice = fx.Login("Alice");
        FakeConnection bob = fx.Login("Bob");
        Say(fx, alice, "/friend bob");
        Assert.Contains(Social(alice).Friends, f => f.Name == "Bob" && f.Online);

        bob.FromClient(new LogoutMessage());
        fx.Ticks(2);
        Assert.Contains(alice.Sent.OfType<ChatLineMessage>(), l => l.Channel == ChatChannel.Notice && l.Text == "Bob has left the world.");
        Assert.Contains(Social(alice).Friends, f => f.Name == "Bob" && !f.Online);

        fx.Login("Bob");
        Assert.Contains(alice.Sent.OfType<ChatLineMessage>(), l => l.Text == "Bob has come into the world.");

        alice.FromClient(new LogoutMessage());
        fx.Ticks(2);
        FakeConnection again = fx.Login("Alice");
        fx.Ticks(1);
        Assert.Contains(Social(again).Friends, f => f.Name == "Bob" && f.Online);
    }

    [Fact]
    public void AFriendMustExist()
    {
        var fx = World();
        FakeConnection alice = fx.Login("Alice");
        Say(fx, alice, "/friend nobody");
        Assert.Contains("There's nobody called nobody.", alice.Texts);
    }

    [Fact]
    public void IgnoringSomeoneSilencesThemEverywhere()
    {
        var fx = World();
        FakeConnection alice = fx.Login("Alice");
        FakeConnection bob = fx.Login("Bob");
        Say(fx, alice, "/ignore bob");

        Say(fx, bob, "hello");
        Say(fx, bob, "/w alice psst");
        Assert.DoesNotContain(alice.Sent.OfType<ChatLineMessage>(), l => l.Text == "hello" || l.Text == "psst");
        Assert.Contains(bob.Sent.OfType<ChatLineMessage>(), l => l.Text == "psst"); // Bob never knows

        Say(fx, alice, "/unignore bob");
        Say(fx, bob, "hello again");
        Assert.Contains(alice.Sent.OfType<ChatLineMessage>(), l => l.Text == "hello again");
    }

    [Fact]
    public void AGroupFormsTalksAndBreaksUp()
    {
        var fx = World();
        FakeConnection alice = fx.Login("Alice");
        FakeConnection bob = fx.Login("Bob");
        FakeConnection carol = fx.Login("Carol");
        SocialSystem social = fx.World.Get<SocialSystem>();

        Say(fx, alice, "/invite bob");
        Assert.Equal("Alice", Social(bob).InvitedBy);
        Say(fx, bob, "/accept");
        Assert.Same(social.GroupOf(fx.Player("Alice")), social.GroupOf(fx.Player("Bob")));
        Assert.Equal(new[] { "Alice", "Bob" }, Social(alice).Group.Select(m => m.Name));
        Assert.True(Social(bob).Group[0].Leader);

        Say(fx, bob, "//pull the next one");
        Assert.Equal("[Group] Bob", Assert.Single(Lines(alice, ChatChannel.Group)).From);
        Assert.Empty(Lines(carol, ChatChannel.Group));

        Say(fx, carol, "/invite alice");
        Assert.Contains("Alice is already in a group.", carol.Texts);
        Say(fx, bob, "/invite carol");
        Assert.Contains("Only Alice can invite to the group.", bob.Texts);

        Say(fx, alice, "/leave");
        Assert.Null(social.GroupOf(fx.Player("Bob"))); // a group of one is no group
        Assert.Empty(Social(bob).Group);
    }

    [Fact]
    public void TheLeaderLeavingHandsTheGroupOn()
    {
        var fx = World();
        FakeConnection alice = fx.Login("Alice");
        FakeConnection bob = fx.Login("Bob");
        FakeConnection carol = fx.Login("Carol");
        Say(fx, alice, "/invite bob");
        Say(fx, bob, "/accept");
        Say(fx, alice, "/invite carol");
        Say(fx, carol, "/accept");

        alice.FromClient(new LogoutMessage());
        fx.Ticks(2);
        SocialSystem social = fx.World.Get<SocialSystem>();
        Assert.Equal("Bob", social.GroupOf(fx.Player("Carol"))!.Leader.Name);
        Assert.Contains(bob.Sent.OfType<ChatLineMessage>(), l => l.Text == "You lead the group now.");
    }

    [Fact]
    public void AnInvitationLapses()
    {
        var fx = World();
        FakeConnection alice = fx.Login("Alice");
        FakeConnection bob = fx.Login("Bob");
        Say(fx, alice, "/invite bob");
        fx.Ticks(SocialSystem.InviteTicks + 1);
        Say(fx, bob, "/accept");
        Assert.Contains("No invitation is waiting.", bob.Texts);
    }

    [Fact]
    public void AnUnknownSlashCommandIsJustSaid()
    {
        var fx = World();
        FakeConnection alice = fx.Login("Alice");
        FakeConnection bob = fx.Login("Bob");
        Say(fx, alice, "/shrug");
        Assert.Contains(bob.Sent.OfType<ChatLineMessage>(), l => l.Channel == ChatChannel.Public && l.Text == "/shrug");
    }
}
