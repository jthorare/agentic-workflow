using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Connections;

namespace Aw.Sindri.Ui.Tests.Connections;

/// <summary>What a service tile says and offers in each state (#2537): a status chip, one primary button, the menu, and the one help line on the client id and token inputs.</summary>
[Lane(TestLane.Unit)]
public sealed class ConnectionTileStatesTests
{
    private readonly ConnectionsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ServiceTileViewModel> Tile(string id) => (await _f.Shown()).Tiles.Single(t => string.Equals(t.Id, id, StringComparison.Ordinal));

    [Fact]
    public async Task Each_state_reads_as_one_chip_and_a_reason_when_it_needs_you()
    {
        _ = _f.Server.SeedConnection(FakeSindriApi.ConnectionRow("linear:work"));
        _ = _f.Server.SeedConnection(FakeSindriApi.ConnectionRow("github:work", "needs-sign-in", "oauth", "the token expired"));
        var page = await _f.Shown();
        ServiceTileViewModel Of(string id) => page.Tiles.Single(t => string.Equals(t.Id, id, StringComparison.Ordinal));

        Assert.Equal(("Connected", string.Empty), (Of("linear").StatusChip, Of("linear").ReasonText));
        Assert.Equal(("Needs reconnect", "the token expired"), (Of("github").StatusChip, Of("github").ReasonText));
        Assert.Equal(("Not connected", string.Empty), (Of("slack").StatusChip, Of("slack").ReasonText));
    }

    [Fact]
    public async Task A_tile_has_one_primary_action_and_a_connected_tile_has_none()
    {
        _ = _f.Server.SeedConnection(FakeSindriApi.ConnectionRow("linear:work"));
        _ = _f.Server.SeedConnection(FakeSindriApi.ConnectionRow("github:work", "needs-sign-in", "oauth", "the token expired"));
        var page = await _f.Shown();
        string Primary(string id) => page.Tiles.Single(t => string.Equals(t.Id, id, StringComparison.Ordinal)).PrimaryText;

        Assert.Equal(["Reconnect", string.Empty, "Add token", "Connect", "Connect"], [Primary("github"), Primary("linear"), Primary("slack"), Primary("google"), Primary("spotify")], StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_failed_sign_in_turns_the_primary_action_into_Try_again()
    {
        var tile = await Tile("github");
        await tile.Connect(Token);
        tile.ClientId = "client-123";
        tile.ClientSecret.Text = "secret-456";
        _f.Server.ConnectionWriteError = new Kernel.Error("SND-CONN-006", Kernel.ErrorKind.Unavailable, "The connection's secret store is not available");

        await tile.Primary(Token);

        Assert.Equal("Try again", tile.PrimaryText);
    }

    [Fact]
    public async Task The_primary_action_asks_first_and_says_why_it_is_disabled()
    {
        var tile = await Tile("github");
        Assert.True(tile.CanPress);

        await tile.Primary(Token);

        Assert.True(tile.IsAsking);
        Assert.False(tile.CanPress);
        Assert.Equal("Enter your GitHub app's client id", tile.BlockedReason);
        tile.ClientId = "client-123";
        Assert.False(tile.CanPress);
        Assert.Equal("Paste your GitHub app's client secret", tile.BlockedReason);
        tile.ClientSecret.Text = "secret-456";
        Assert.True(tile.CanPress);
        Assert.Equal(string.Empty, tile.BlockedReason);
    }

    [Fact]
    public async Task A_client_id_input_has_one_help_line_and_a_link_to_the_providers_OAuth_apps()
    {
        var tile = await Tile("linear");

        Assert.DoesNotContain('\n', tile.HelpText);
        Assert.Equal("Create an OAuth app in Linear, then paste its client id here.", tile.HelpText);
        Assert.Equal(("Open Linear's API settings", "https://linear.app/settings/api"), (tile.HelpLinkText, tile.HelpUrl));

        await tile.OpenHelp(Token);

        Assert.Equal(["https://linear.app/settings/api"], _f.Launcher.Opened, StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("github", "https://github.com/settings/developers")]
    [InlineData("google", "https://console.cloud.google.com/apis/credentials")]
    [InlineData("spotify", "https://developer.spotify.com/dashboard")]
    public async Task Every_sign_in_tile_links_to_where_its_client_id_is_made(string id, string url)
    {
        var tile = await Tile(id);

        Assert.Equal(url, tile.HelpUrl);
        Assert.NotEmpty(tile.HelpText);
    }

    [Fact]
    public async Task The_Slack_tile_is_Add_token_with_an_inline_field_and_one_line_on_where_the_token_comes_from()
    {
        var tile = await Tile("slack");

        Assert.Equal("Add token", tile.PrimaryText);
        Assert.Equal("In Slack, open your app's OAuth & Permissions page and copy the user token.", tile.HelpText);
        Assert.Equal(string.Empty, tile.HelpUrl);

        await tile.Primary(Token);

        Assert.True(tile.IsAsking);
        Assert.True(tile.TakesToken);
        Assert.Equal("Paste a Slack token", tile.BlockedReason);
    }

    [Fact]
    public async Task The_menu_offers_Reconnect_on_a_browser_sign_in_and_Reconnect_signs_in_again()
    {
        _ = _f.Server.SeedConnection(FakeSindriApi.ConnectionRow("linear:work"));
        _ = _f.Server.SeedConnection(FakeSindriApi.ConnectionRow("slack:work", "connected", "rest"));
        var page = await _f.Shown();
        var linear = page.Tiles.Single(t => string.Equals(t.Id, "linear", StringComparison.Ordinal));

        Assert.True(linear.CanReconnect);
        Assert.False(page.Tiles.Single(t => string.Equals(t.Id, "slack", StringComparison.Ordinal)).CanReconnect);   // a pasted token has no browser sign-in
        Assert.False(page.Tiles.Single(t => string.Equals(t.Id, "github", StringComparison.Ordinal)).CanReconnect);   // not connected: nothing to reconnect

        await _f.Drive(linear.SignInAgain(Token), "linear:work");

        Assert.Contains("signin linear:work", _f.Server.ConnectionCallLog, StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_needs_reconnect_tile_runs_its_sign_in_from_the_primary_action()
    {
        _ = _f.Server.SeedConnection(FakeSindriApi.ConnectionRow("github:work", "needs-sign-in", "oauth", "the token expired"));
        var page = await _f.Shown();
        var tile = page.Tiles.Single(t => string.Equals(t.Id, "github", StringComparison.Ordinal));

        await _f.Drive(tile.Primary(Token), "github:work");

        Assert.Contains("signin github:work", _f.Server.ConnectionCallLog, StringComparer.Ordinal);
        Assert.Equal("Connected", tile.StatusChip);
    }

    [Fact]
    public async Task The_custom_form_offers_no_browser_sign_in_provider()
    {
        var form = (await _f.Shown()).Add;

        Assert.Equal(["mcp", "rest", "cli"], form.Presets.Select(p => p.Id), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_custom_server_opens_the_consent_page_and_waits_until_it_reads_connected()
    {
        var page = await _f.Shown();
        var form = page.Add;
        form.Open();
        form.SelectedPreset = form.Presets.Single(p => string.Equals(p.Id, "mcp", StringComparison.Ordinal));
        form.Name = "notes:mcp";
        form.Url = "https://notes.example.test/mcp";

        await _f.Drive(form.Send(Token), "notes:mcp");

        Assert.Equal([_f.Server.ConsentUrl], _f.Launcher.Opened, StringComparer.Ordinal);
        Assert.Equal(ViewModels.Labels.ConnectionAdded("notes:mcp"), form.StatusText);
        Assert.Equal(["notes:mcp"], page.OtherRows.Select(r => r.Name), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_GitHub_tile_sends_the_client_id_and_the_client_secret_and_the_secret_is_gone_after()
    {
        var page = await _f.Shown();
        var tile = page.Tiles.Single(t => string.Equals(t.Id, "github", StringComparison.Ordinal));
        await tile.Connect(Token);
        Assert.True(tile.NeedsSecret);
        Assert.Equal("Paste the client ID and secret from your GitHub OAuth app.", tile.HelpText);
        tile.ClientId = "client-123";
        tile.ClientSecret.Text = "secret-456";

        await _f.Drive(tile.Connect(Token), "github:work");

        var sent = _f.Server.LastAddConnection!;
        Assert.Equal(("github:work", "github", "client-123", "secret-456"), (sent.Name, sent.Preset, sent.Client, sent.ClientSecret));
        Assert.False(tile.ClientSecret.HasValue);
        Assert.True(tile.IsConnected);
    }

    [Fact]
    public async Task A_Google_tile_also_asks_for_the_secret_and_Linear_and_Spotify_do_not()
    {
        var page = await _f.Shown();

        Assert.Equal(["github", "google"], page.Tiles.Where(t => t.NeedsSecret).Select(t => t.Id), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_Linear_tile_sends_no_client_secret()
    {
        var page = await _f.Shown();
        var tile = page.Tiles.Single(t => string.Equals(t.Id, "linear", StringComparison.Ordinal));
        await tile.Connect(Token);
        tile.ClientId = "client-123";
        Assert.True(tile.CanPress);

        await _f.Drive(tile.Connect(Token), "linear:work");

        Assert.Null(_f.Server.LastAddConnection!.ClientSecret);
    }

    [Fact]
    public async Task The_client_secret_is_in_no_text_of_the_tile()
    {
        var page = await _f.Shown();
        var tile = page.Tiles.Single(t => string.Equals(t.Id, "github", StringComparison.Ordinal));
        await tile.Connect(Token);
        tile.ClientId = "client-123";
        tile.ClientSecret.Text = "secret-9f3a7c21";
        _f.Server.ConnectionWriteError = new Kernel.Error("SND-CONN-010", Kernel.ErrorKind.Conflict, "A connection named github:work already exists.");

        await tile.Connect(Token);

        string[] shown = [tile.ClientSecret.ToString(), tile.BlockedReason, tile.ErrorText, tile.StatusText, tile.AccessibleName, tile.HelpText, tile.Subtitle, tile.ClientId, tile.ReasonText];
        Assert.DoesNotContain(shown, t => t.Contains("secret-9f3a7c21", StringComparison.Ordinal));
        Assert.False(tile.ClientSecret.HasValue);
    }
}
