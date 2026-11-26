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
    public async Task The_GitHub_token_has_three_steps_with_one_permission_per_line_and_a_link_to_the_token_page()
    {
        var tile = await Tile("github");
        await tile.Connect(Token);

        Assert.True(tile.ShowTokenSteps);
        Assert.Equal(["1. ", "2. ", "3. "], tile.TokenSteps.Where(s => !s.IsDetail).Select(s => s.Text[..3]), StringComparer.Ordinal);
        Assert.Equal(
            ["Pull requests: read and write", "Contents: read and write", "Checks: read", "Issues: read", "Metadata: read"],
            tile.TokenSteps.Skip(2).Take(5).Where(s => s.IsDetail).Select(s => s.Text),
            StringComparer.Ordinal);
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
    [InlineData("SND-CONN-018", "Linear refused the token.")]
    [InlineData("SND-CONN-019", "Couldn't reach Linear to check the token.")]
    [InlineData("SND-CONN-020", "Linear is limiting requests right now, so the token wasn't checked. Wait a minute, then try again.")]
    public async Task A_refused_or_unchecked_token_names_the_service_in_words_and_no_code(string code, string words)
    {
        var tile = await Tile("linear");
        await tile.Connect(Token);
        tile.Token.Text = "lin_api_test_0001";
        _f.Server.ConnectionWriteError = new Kernel.Error(code, Kernel.ErrorKind.Invalid, "Linear refused the key");

        await tile.Connect(Token);

        Assert.StartsWith(words, tile.ErrorText, StringComparison.Ordinal);
        Assert.DoesNotContain("SND-", tile.ErrorText, StringComparison.Ordinal);
    }

    // #4428: only a refused key (018) is the token's fault; 019 and 020 never judged it, so the pasted token stays, Try again is live, and the line is amber, not red.
    [Theory]
    [InlineData("SND-CONN-018", false, false)]
    [InlineData("SND-CONN-019", true, true)]
    [InlineData("SND-CONN-020", true, true)]
    public async Task A_check_that_never_judged_the_token_keeps_it_and_shows_amber_only_a_refused_token_clears(string code, bool kept, bool caution)
    {
        var tile = await Tile("github");
        await tile.Connect(Token);
        tile.Token.Text = "github_pat_test_0001";
        _f.Server.ConnectionWriteError = new Kernel.Error(code, Kernel.ErrorKind.Invalid, "GitHub did not take the key");

        await tile.Connect(Token);

        Assert.Equal((kept, caution), (tile.Token.HasValue, tile.ErrorIsCaution));
        Assert.Equal(kept ? ("Try again", true) : ("Add token", false), (tile.PrimaryText, tile.CanPress));
    }

    [Fact]
    public async Task Try_again_after_a_rate_limit_sends_the_same_token_and_clears_it_once_connected()
    {
        var tile = await Tile("github");
        await tile.Connect(Token);
        tile.Token.Text = "github_pat_test_0001";
        _f.Server.ConnectionWriteError = new Kernel.Error("SND-CONN-020", Kernel.ErrorKind.RateLimited, "rate limited");
        await tile.Connect(Token);
        _f.Server.ConnectionWriteError = null;

        await tile.Connect(Token);

        Assert.Equal(["github_pat_test_0001", "github_pat_test_0001"], _f.Server.AddedConnections.Select(b => b.Key), StringComparer.Ordinal);
        Assert.Equal((string.Empty, false, false), (tile.ErrorText, tile.ErrorIsCaution, tile.Token.HasValue));
    }

    [Fact]
    public async Task After_a_refused_token_the_button_reads_Add_token_until_a_new_token_is_pasted_never_a_greyed_Try_again()
    {
        var tile = await Tile("github");
        await tile.Connect(Token);
        tile.Token.Text = "github_pat_test_0001";
        _f.Server.ConnectionWriteError = new Kernel.Error("SND-CONN-018", Kernel.ErrorKind.Invalid, "GitHub refused the key");

        await tile.Connect(Token);

        Assert.Equal(("Add token", false), (tile.PrimaryText, tile.CanPress));
        tile.Token.Text = "github_pat_test_0002";
        Assert.Equal(("Add token", true), (tile.PrimaryText, tile.CanPress));
    }

    // #4368: step 3 already says to paste, so the waiting line under the links stays hidden; the button's tooltip still says why it waits.
    [Theory]
    [InlineData("github")]
    [InlineData("linear")]
    [InlineData("slack")]
    public async Task Under_the_token_steps_the_waiting_line_is_hidden_and_the_tooltip_keeps_it(string id)
    {
        var tile = await Tile(id);
        await tile.Connect(Token);

        Assert.False(tile.HasBlockedReason);
        Assert.StartsWith("Paste a ", tile.BlockedReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_OAuth_fields_say_client_ID_and_the_help_line_says_to_paste_it_once()
    {
        var tile = await Tile("linear");
        await tile.Connect(Token);
        tile.UseOAuth();

        Assert.Equal(("Client ID", "Enter your Linear app's client ID", false), (tile.Text.TileClientLabel, tile.BlockedReason, tile.HasBlockedReason));   // #4396: the help line says to paste the client ID, so the inline reason stays quiet
        Assert.Contains("client ID", tile.HelpText, StringComparison.Ordinal);
    }
}
