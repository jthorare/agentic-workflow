using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Connections;

namespace Aw.Sindri.Ui.Tests.Connections;

/// <summary>#4357: GitHub, Linear and Slack tiles take a pasted token first, under three steps; GitHub and Linear can swap to their own OAuth app; Google and Spotify sign in only.</summary>
[Lane(TestLane.Unit)]
public sealed class ConnectionTokenFirstTests
{
    private readonly ConnectionsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ServiceTileViewModel> Tile(string id) => (await _f.Shown()).Tiles.Single(t => string.Equals(t.Id, id, StringComparison.Ordinal));

    [Fact]
    public async Task GitHub_Linear_and_Slack_ask_for_a_token_and_Google_and_Spotify_for_a_client_id()
    {
        var page = await _f.Shown();
        foreach (var tile in page.Tiles)
        {
            await tile.Connect(Token);
        }

        Assert.Equal(["github", "linear", "slack"], page.Tiles.Where(t => t.TakesToken).Select(t => t.Id), StringComparer.Ordinal);
        Assert.Equal(["google", "spotify"], page.Tiles.Where(t => t.NeedsClientId).Select(t => t.Id), StringComparer.Ordinal);
        Assert.Equal(["github", "linear"], page.Tiles.Where(t => t.ShowUseOAuth).Select(t => t.Id), StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_GitHub_token_has_three_steps_naming_the_fine_grained_set_and_a_link_to_the_token_page()
    {
        var tile = await Tile("github");
        await tile.Connect(Token);

        Assert.True(tile.ShowTokenSteps);
        Assert.Equal(3, tile.TokenSteps.Count);
        Assert.Contains("Pull requests: read and write, Contents: read and write, Checks: read, Issues: read, Metadata: read", tile.TokenSteps[1], StringComparison.Ordinal);
        Assert.Equal(("Add token", "https://github.com/settings/personal-access-tokens/new"), (tile.PrimaryText, tile.HelpUrl));
        Assert.False(tile.HasHelpText);
    }

    [Fact]
    public async Task A_GitHub_token_is_sent_once_as_a_rest_key_of_the_github_preset_and_is_gone_after()
    {
        var tile = await Tile("github");
        await tile.Connect(Token);
        tile.Token.Text = "github_pat_test_0001";

        await _f.Drive(tile.Connect(Token), "github:work");

        var sent = _f.Server.LastAddConnection!;
        Assert.Equal(("github:work", "github", "rest", "github_pat_test_0001", null), (sent.Name, sent.Preset, sent.Kind, sent.Key, sent.Client));
        Assert.False(tile.Token.HasValue);
    }

    [Fact]
    public async Task The_swap_to_the_OAuth_app_drops_the_pasted_token_and_the_swap_back_drops_the_client_fields()
    {
        var tile = await Tile("github");
        await tile.Connect(Token);
        tile.Token.Text = "github_pat_test_0001";

        tile.UseOAuth();

        Assert.False(tile.Token.HasValue);
        Assert.Equal((true, true, false, true), (tile.NeedsClientId, tile.NeedsSecret, tile.ShowTokenSteps, tile.ShowUseToken));
        Assert.Equal("https://github.com/settings/developers", tile.HelpUrl);
        tile.ClientId = "client-123";
        tile.ClientSecret.Text = "secret-456";

        tile.UseToken();

        Assert.Equal((string.Empty, false, true), (tile.ClientId, tile.ClientSecret.HasValue, tile.TakesToken));
        Assert.Equal("Paste a GitHub token", tile.BlockedReason);
    }

    [Fact]
    public async Task Cancel_goes_back_to_the_token_for_the_next_Connect()
    {
        var tile = await Tile("linear");
        await tile.Connect(Token);
        tile.UseOAuth();

        tile.Cancel();
        await tile.Connect(Token);

        Assert.True(tile.TakesToken);
    }

    [Theory]
    [InlineData("slack")]
    [InlineData("google")]
    [InlineData("spotify")]
    public async Task Slack_Google_and_Spotify_offer_no_swap(string id)
    {
        var tile = await Tile(id);
        await tile.Connect(Token);

        Assert.False(tile.ShowUseOAuth);
        Assert.False(tile.ShowUseToken);
    }

    [Theory]
    [InlineData("SND-CONN-018", "The service refused that token.")]
    [InlineData("SND-CONN-019", "Couldn't reach the service to check the token.")]
    public async Task A_refused_or_unchecked_token_says_so_in_words_and_no_code(string code, string words)
    {
        var tile = await Tile("linear");
        await tile.Connect(Token);
        tile.Token.Text = "lin_api_test_0001";
        _f.Server.ConnectionWriteError = new Kernel.Error(code, Kernel.ErrorKind.Invalid, "Linear refused the key");

        await tile.Connect(Token);

        Assert.StartsWith(words, tile.ErrorText, StringComparison.Ordinal);
        Assert.DoesNotContain("SND-", tile.ErrorText, StringComparison.Ordinal);
        Assert.False(tile.Token.HasValue);
    }
}
