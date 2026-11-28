using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Connections;

namespace Aw.Sindri.Ui.Tests.Connections;

/// <summary>#4358: a connected key tile (rest or cli) offers "Replace token". The new key goes to the server once; a refusal keeps the tile connected on its old key and says so; a good key closes the box.</summary>
[Lane(TestLane.Unit)]
public sealed class ReplaceTokenTests
{
    private const string Fresh = "ghp_new_token_0002";
    private const string NoteWords = "Token replaced. Sindri can't check this kind of token, so try it once to make sure it works.";

    private readonly ConnectionsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ServiceTileViewModel> Tile(string id = "github", string kind = "rest")
    {
        _ = _f.Server.SeedConnection(FakeSindriApi.ConnectionRow("github:work", kind: kind));
        _ = _f.Server.SeedConnection(FakeSindriApi.ConnectionRow("linear:work"));
        return (await _f.Shown()).Tiles.Single(t => string.Equals(t.Id, id, StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_link_shows_on_a_connected_key_tile_and_not_on_an_oauth_tile_or_an_unconnected_one()
    {
        var github = await Tile();
        var page = await _f.Shown();

        Assert.True(github.ShowReplaceLink);
        Assert.False(page.Tiles.Single(t => string.Equals(t.Id, "linear", StringComparison.Ordinal)).ShowReplaceLink);
        Assert.False(page.Tiles.Single(t => string.Equals(t.Id, "slack", StringComparison.Ordinal)).ShowReplaceLink);
    }

    [Fact]
    public async Task A_good_token_is_sent_once_closes_the_box_and_leaves_the_tile_connected()
    {
        var tile = await Tile();
        tile.StartReplace();
        Assert.True(tile.IsReplacing);
        Assert.False(tile.CanSendReplace);
        tile.ReplaceToken.Text = Fresh;
        Assert.True(tile.CanSendReplace);

        await tile.Replace(Token);

        Assert.Equal(Fresh, Assert.Single(_f.Server.ReplacedKeys).Key);
        Assert.Contains("replace-key github:work", _f.Server.ConnectionCallLog, StringComparer.Ordinal);
        Assert.False(tile.IsReplacing);
        Assert.False(tile.ReplaceToken.HasValue);
        Assert.False(tile.HasReplaceError);
        Assert.True(tile.IsConnected);
        Assert.True(tile.ShowReplaceLink);
    }

    [Fact]
    public async Task A_token_stored_unchecked_says_so_on_the_tile_until_the_box_opens_again()
    {
        var tile = await Tile(kind: "cli");
        _f.Server.ReplaceKeyUnchecked = true;
        tile.StartReplace();
        tile.ReplaceToken.Text = Fresh;

        await tile.Replace(Token);

        Assert.Equal(NoteWords, tile.ReplaceNote);
        Assert.Equal("a cli connection has no service to check the key against", tile.ReplaceNoteTip);
        Assert.True(tile.HasReplaceNote);
        tile.StartReplace();
        Assert.False(tile.HasReplaceNote);
    }

    [Theory]
    [InlineData("an mcp connection has no key check")]
    [InlineData("this connection has no preset, so there is no service to check the key against")]
    [InlineData("Acme has no key check")]
    public async Task The_note_is_the_same_product_words_whatever_the_server_said_and_the_reason_is_its_tooltip(string reason)
    {
        var tile = await Tile();
        (_f.Server.ReplaceKeyUnchecked, _f.Server.ReplaceKeyUncheckedReason) = (true, reason);
        tile.StartReplace();
        tile.ReplaceToken.Text = Fresh;

        await tile.Replace(Token);

        Assert.Equal(NoteWords, tile.ReplaceNote);
        Assert.DoesNotContain(reason, tile.ReplaceNote, StringComparison.Ordinal);
        Assert.DoesNotContain("key", tile.ReplaceNote, StringComparison.Ordinal);
        Assert.Contains("token", tile.ReplaceNote, StringComparison.Ordinal);
        Assert.Equal(reason, tile.ReplaceNoteTip);
    }

    [Fact]
    public async Task A_checked_token_leaves_no_note()
    {
        var tile = await Tile();
        tile.StartReplace();
        tile.ReplaceToken.Text = Fresh;

        await tile.Replace(Token);

        Assert.False(tile.HasReplaceNote);
    }

    [Fact]
    public async Task A_refused_token_keeps_the_box_open_the_tile_connected_and_says_the_old_token_is_in_use()
    {
        var tile = await Tile();
        _f.Server.NextReplaceKeyError = new Error("SND-CONN-018", ErrorKind.Invalid, "GitHub refused the key");
        tile.StartReplace();
        tile.ReplaceToken.Text = Fresh;

        await tile.Replace(Token);

        Assert.True(tile.IsReplacing);
        Assert.True(tile.IsConnected);
        Assert.True(tile.HasReplaceError);
        Assert.Contains("refused the new token", tile.ReplaceError, StringComparison.Ordinal);
        Assert.Contains("old one is still in use", tile.ReplaceError, StringComparison.Ordinal);
        Assert.DoesNotContain("SND-", tile.ReplaceError, StringComparison.Ordinal);
        Assert.False(tile.ReplaceErrorIsCaution);
        Assert.False(tile.ReplaceToken.HasValue);   // a refused token is forgotten: paste another
    }

    [Fact]
    public async Task A_check_that_never_judged_the_token_shows_amber_and_keeps_it_in_the_box_for_another_try()
    {
        var tile = await Tile();
        _f.Server.NextReplaceKeyError = new Error("SND-CONN-019", ErrorKind.Unavailable, "no answer");
        tile.StartReplace();
        tile.ReplaceToken.Text = Fresh;

        await tile.Replace(Token);

        Assert.True(tile.ReplaceErrorIsCaution);
        Assert.True(tile.ReplaceToken.HasValue);
        Assert.True(tile.CanSendReplace);

        await tile.Replace(Token);

        Assert.Equal([Fresh, Fresh], _f.Server.ReplacedKeys.Select(k => k.Key), StringComparer.Ordinal);
        Assert.False(tile.IsReplacing);
    }

    [Fact]
    public async Task While_the_token_is_checked_the_tile_says_so_and_a_second_press_sends_nothing()
    {
        var tile = await Tile();
        _f.Server.ReplaceKeyGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        tile.StartReplace();
        tile.ReplaceToken.Text = Fresh;

        var running = tile.Replace(Token);

        Assert.True(tile.IsReplaceChecking);
        Assert.Equal("Checking the new token...", tile.ReplaceStatus);
        Assert.False(tile.CanSendReplace);
        await tile.Replace(Token);
        tile.CancelReplace();
        Assert.True(tile.IsReplacing);

        _f.Server.ReplaceKeyGate.SetResult();
        await running;

        _ = Assert.Single(_f.Server.ReplacedKeys);
        Assert.False(tile.IsReplaceChecking);
        Assert.False(tile.IsReplacing);
    }

    [Fact]
    public async Task Cancel_forgets_the_pasted_token_and_sends_nothing()
    {
        var tile = await Tile();
        tile.StartReplace();
        tile.ReplaceToken.Text = Fresh;

        tile.CancelReplace();

        Assert.False(tile.IsReplacing);
        Assert.False(tile.ReplaceToken.HasValue);
        Assert.Empty(_f.Server.ReplacedKeys);
    }

    [Fact]
    public async Task An_oauth_tile_cannot_open_the_box()
    {
        var tile = await Tile("linear");

        tile.StartReplace();

        Assert.False(tile.IsReplacing);
    }
}
