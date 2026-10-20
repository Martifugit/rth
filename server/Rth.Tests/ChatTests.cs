using Rth.Server.Game;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Tests;

public class ChatTests
{
    [Fact]
    public void PublicChatReachesEveryoneInView()
    {
        var fx = new WorldFixture(Maps.Open(64, 64, new TileCoord(5, 5)));
        FakeConnection alice = fx.Login("Alice");
        FakeConnection bob = fx.Login("Bob");
        FakeConnection carol = fx.Login("Carol");
        carol.FromClient(new WalkToMessage { X = 50, Y = 50, Run = true });
        fx.Ticks(30);

        alice.FromClient(new ChatMessage { Text = "  hello   there\n" });
        fx.Ticks(1);

        ChatLineMessage heard = bob.Sent.OfType<ChatLineMessage>().Single();
        Assert.Equal(("Alice", "hello there", fx.Player("Alice").Id), (heard.From, heard.Text, heard.EntityId));
        Assert.Single(alice.Sent.OfType<ChatLineMessage>()); // you see your own line
        Assert.Empty(carol.Sent.OfType<ChatLineMessage>());  // too far away
    }

    [Fact]
    public void ChatIsCleanedAndCapped()
    {
        Assert.Equal("a b", ChatSystem.Clean("\ta \u0007 b  "));
        Assert.Equal(ChatMessage.MaxLength, ChatSystem.Clean(new string('x', 500)).Length);
        Assert.Equal("", ChatSystem.Clean("   "));
    }
}
