using Aw.Kernel.Testing;

namespace Aw.Sindri.Ui.Tests.Connections;

/// <summary>The Connections page opens on service tiles (#2537): tiles are the empty state, the custom form is secondary, a disabled Add says why, and the default view has no jargon.</summary>
[Lane(TestLane.Unit)]
public sealed class ConnectionTilesTests
{
    private static readonly string[] _jargon = ["MCP server", "service:label", "Server address", "instead of a token"];

    private readonly ConnectionsFixture _f = new();

    [Fact]
    public async Task The_default_view_is_the_service_tiles_and_the_custom_form_is_closed()
    {
        var page = await _f.Shown();

        Assert.Equal(["github", "linear", "slack", "google", "spotify"], page.Tiles.Select(t => t.Id), StringComparer.Ordinal);
        Assert.False(page.Add.IsOpen);
    }

    [Fact]
    public async Task An_empty_page_shows_the_tiles_and_no_empty_box_and_no_count()
    {
        var page = await _f.Shown();

        Assert.False(page.IsEmpty);
        Assert.Equal(string.Empty, page.EmptyText);
        Assert.Equal(string.Empty, page.Head);
    }

    [Fact]
    public async Task A_disabled_Add_carries_a_reason()
    {
        var page = await _f.Shown();
        page.Add.Open();

        Assert.False(page.Add.CanSend);
        Assert.Equal("Enter a server address", page.Add.SendBlockedReason);
    }

    [Fact]
    public async Task The_default_view_has_no_jargon()
    {
        var page = await _f.Shown();
        var words = new List<string> { page.Text.Title, page.Text.Purpose, page.Head, page.EmptyText };
        words.AddRange(page.Tiles.SelectMany(t => new[] { t.Title }));

        foreach (var word in _jargon)
        {
            Assert.DoesNotContain(words, w => w.Contains(word, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Connect_asks_for_the_client_id_first_and_says_why_it_waits()
    {
        var page = await _f.Shown();
        var tile = page.Tiles.Single(t => t.Id == "linear");

        await tile.Connect(Token);

        Assert.True(tile.IsAsking);
        Assert.False(tile.CanSend);
        Assert.Equal("Enter your Linear app's client id", tile.BlockedReason);
        Assert.Empty(_f.Server.AddedConnections);
    }

    [Fact]
    public async Task Connect_names_the_connection_itself_opens_the_browser_and_shows_the_connected_tile()
    {
        var page = await _f.Shown();
        var tile = page.Tiles.Single(t => t.Id == "linear");
        await tile.Connect(Token);
        tile.ClientId = "client-123";

        await _f.Drive(tile.Connect(Token), "linear:work");

        Assert.Equal([_f.Server.ConsentUrl], _f.Launcher.Opened, StringComparer.Ordinal);
        Assert.Equal(("linear:work", "linear", "client-123"), (_f.Server.LastAddConnection!.Name, _f.Server.LastAddConnection.Preset, _f.Server.LastAddConnection.Client));
        Assert.True(tile.IsConnected);
        Assert.Equal("work", tile.Account);
        Assert.Equal("1 of 5 connected", page.Head);
        Assert.Empty(page.OtherRows);
    }

    [Fact]
    public async Task A_failed_sign_in_shows_on_its_tile_with_Try_again()
    {
        var page = await _f.Shown();
        var tile = page.Tiles.Single(t => t.Id == "github");
        await tile.Connect(Token);
        tile.ClientId = "client-123";
        _f.Server.NextConnectionGetError = new Kernel.Error("SND-CONN-003", Kernel.ErrorKind.Unavailable, "The provider refused the sign-in.");

        await _f.Drive(tile.Connect(Token));

        Assert.True(tile.HasError);
        Assert.Equal("Try again", tile.PrimaryText);
        Assert.False(page.Tiles.Single(t => t.Id == "linear").HasError);
    }

    [Fact]
    public async Task A_keychain_failure_names_the_keychain_and_no_code()
    {
        var page = await _f.Shown();
        var tile = page.Tiles.Single(t => t.Id == "github");
        await tile.Connect(Token);
        tile.ClientId = "client-123";
        _f.Server.ConnectionWriteError = new Kernel.Error("SND-CONN-006", Kernel.ErrorKind.Unavailable, "The connection's secret store is not available");

        await tile.Connect(Token);

        Assert.Contains("keychain", tile.ErrorText, StringComparison.Ordinal);
        Assert.DoesNotContain("SND-", tile.ErrorText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Slack_takes_a_pasted_token_and_the_token_is_gone_after_the_request()
    {
        var page = await _f.Shown();
        var tile = page.Tiles.Single(t => t.Id == "slack");
        await tile.Connect(Token);
        Assert.Equal("Paste a Slack token", tile.BlockedReason);
        tile.Token.Text = "pasted-test-value";

        await _f.Drive(tile.Connect(Token));

        Assert.Equal(("slack:work", "rest", "pasted-test-value"), (_f.Server.LastAddConnection!.Name, _f.Server.LastAddConnection.Kind, _f.Server.LastAddConnection.Key));
        Assert.False(tile.Token.HasValue);
    }

    [Fact]
    public async Task A_connected_tile_shows_the_account_and_Disconnect_removes_the_connection()
    {
        _f.SeedThree();
        var page = await _f.Shown();
        var tile = page.Tiles.Single(t => t.Id == "linear");

        Assert.Equal(("work", true, "Last used 30m ago"), (tile.Account, tile.IsConnected, tile.LastUsedText));

        await tile.Disconnect(Token);

        Assert.False(tile.IsConnected);
        Assert.Equal("Connect", tile.PrimaryText);
    }
}
