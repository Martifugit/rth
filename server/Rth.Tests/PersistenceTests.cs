using Rth.Server.Persistence;

namespace Rth.Tests;

public sealed class PersistenceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rth-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void SaveThenLoadRoundTrips()
    {
        var store = new JsonPlayerStore(_dir);
        store.Save("bob", new PlayerSave { Name = "Bob", Plane = 1, X = 12, Y = -3 });

        PlayerSave? loaded = new JsonPlayerStore(_dir).Load("bob");

        Assert.NotNull(loaded);
        Assert.Equal(("Bob", 1, 12, -3, PlayerSave.CurrentVersion), (loaded!.Name, loaded.Plane, loaded.X, loaded.Y, loaded.Version));
    }

    [Fact]
    public void UnknownPlayerLoadsAsNull()
    {
        Assert.Null(new JsonPlayerStore(_dir).Load("nobody"));
    }

    [Fact]
    public void SavingAgainOverwritesAndLeavesNoTempFiles()
    {
        var store = new JsonPlayerStore(_dir);
        store.Save("bob", new PlayerSave { Name = "Bob", X = 1 });
        store.Save("bob", new PlayerSave { Name = "Bob", X = 2 });

        Assert.Equal(2, store.Load("bob")!.X);
        Assert.Single(Directory.GetFiles(_dir));
    }

    [Fact]
    public void ReservedWindowsNamesStillSave()
    {
        var store = new JsonPlayerStore(_dir);
        store.Save("con", new PlayerSave { Name = "Con", X = 7 });
        Assert.Equal(7, store.Load("con")!.X);
    }

    [Theory]
    [InlineData("Bob", "Bob")]
    [InlineData("  Iron   Man ", "Iron Man")]
    [InlineData("a_b-c", "a_b-c")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("TwelveChars1", "TwelveChars1")]
    [InlineData("ThirteenChars", null)]
    [InlineData("../evil", null)]
    [InlineData("tab\there", null)]
    public void DisplayNamesAreValidated(string raw, string? expected)
    {
        Assert.Equal(expected, PlayerNames.ToDisplayName(raw));
    }

    [Fact]
    public void KeysIgnoreCaseAndSpaces()
    {
        Assert.Equal("iron_man", PlayerNames.ToKey("Iron Man"));
        Assert.Equal(PlayerNames.ToKey("BOB"), PlayerNames.ToKey("bob"));
    }
}
