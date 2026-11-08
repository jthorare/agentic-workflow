using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Features.GetConnection;
using Aw.Sindri.Testing.Connections;

namespace Aw.Sindri.Tests.Connections;

/// <summary>What the review of the first connections PR asked for: the loopback refusal, the background sign-in's token, the key and client secret the CLI sends, the outcome of a failed sign-in.</summary>
[Lane(TestLane.Unit)]
public sealed class ConnectionReviewFixTests
{
    private const string CliKey = "sk-live-CLIKEY-0001";
    private const string CliClientSecret = "cs-live-CLIENT-0001";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ConnectionHarness Slack()
    {
        var slack = ConnectionHarness.Linear with
        {
            Key = "slack",
            DisplayName = "Slack",
            LoopbackAllowed = false,
            SignInNote = "Slack only accepts https redirect URLs; add the connection with --kind rest and a token instead.",
        };
        return new ConnectionHarness { Presets = new FakeConnectionPresets(slack) };
    }

    [Fact]
    public async Task A_preset_that_refuses_a_loopback_redirect_is_refused_before_any_listener_opens_with_its_sign_in_note()
    {
        var world = Slack();

        var added = await world.Add().Handle(ConnectionHarness.Oauth() with { Preset = "slack", Name = "slack:team" }, Token);

        Assert.True(added.IsError);
        Assert.Contains("--kind rest", added.Error.Message, StringComparison.Ordinal);
        Assert.Contains("Slack", added.Error.Message, StringComparison.Ordinal);
        Assert.Empty(world.SignIn.Clients);   // no sign-in was started
        Assert.Empty(world.Store.Rows);
    }

    [Fact]
    public async Task A_background_sign_in_does_not_end_when_the_request_that_started_it_does()
    {
        var world = new ConnectionHarness();
        using var request = new CancellationTokenSource();

        var added = await world.Add().Handle(ConnectionHarness.Oauth(wait: false), request.Token);
        await request.CancelAsync();   // the MCP client closed, or the CLI exited
        world.SignIn.Succeed(new TokenSet("access-1", "refresh-1", ExpiresAt: null));
        var row = await Eventually(() => world.Store.Rows is [var first, ..] ? first : null);

        Assert.True(added.IsSuccess);
        Assert.False(world.SignIn.LastToken.CanBeCanceled);   // only the sign-in's own 5-minute limit applies
        Assert.Equal(ConnectionStatus.Connected, row.Status);
    }

    [Fact]
    public async Task A_waiting_sign_in_is_tied_to_the_request_token()
    {
        var world = new ConnectionHarness();
        using var request = new CancellationTokenSource();

        var pending = world.Add().Handle(ConnectionHarness.Oauth(), request.Token).AsTask();
        _ = await Eventually(() => world.SignIn.Clients.Count > 0 ? world.SignIn : null);

        Assert.True(world.SignIn.LastToken.CanBeCanceled);
        world.SignIn.Succeed(new TokenSet("access-1", "refresh-1", ExpiresAt: null));
        Assert.True((await pending).IsSuccess);
    }

    [Fact]
    public async Task A_key_the_cli_sent_is_stored_without_the_variable_existing_where_serve_runs_and_the_request_never_prints_it()
    {
        var world = new ConnectionHarness();
        var request = ConnectionHarness.Rest() with { KeyEnv = "ONLY_IN_THE_CLI_SHELL", Key = CliKey };

        var added = await world.Add().Handle(request, Token);

        Assert.True(added.IsSuccess, added.IsError ? added.Error.Message : null);
        Assert.Equal(CliKey, world.Secrets.Tokens(Assert.Single(world.Store.Rows).SecretPointer!.ToString())!.AccessToken);
        Assert.DoesNotContain(CliKey, request.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(CliKey, System.Text.Json.JsonSerializer.Serialize(added.Value, Host.SindriJsonContext.Default.AddConnectionResponse), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_key_env_name_that_serve_cannot_read_says_whose_environment_it_looked_in()
    {
        var world = new ConnectionHarness();

        var added = await world.Add().Handle(ConnectionHarness.Rest(keyEnv: "NOPE_KEY"), Token);

        Assert.True(added.IsError);
        Assert.Contains("NOPE_KEY", added.Error.Message, StringComparison.Ordinal);
        Assert.Contains("running Sindri", added.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_client_secret_goes_to_the_sign_in_and_into_the_keychain_secret_with_the_tokens_and_into_no_row()
    {
        var world = new ConnectionHarness();
        var pending = world.Add().Handle(ConnectionHarness.Oauth() with { ClientSecret = CliClientSecret }, Token).AsTask();
        world.SignIn.Succeed(new TokenSet("access-1", "refresh-1", ExpiresAt: null));

        var added = await pending;

        Assert.True(added.IsSuccess, added.IsError ? added.Error.Message : null);
        Assert.Equal(CliClientSecret, world.SignIn.LastClientSecret);
        var row = Assert.Single(world.Store.Rows);
        Assert.Equal(CliClientSecret, world.Secrets.Tokens(row.SecretPointer!.ToString())!.ClientSecret);
        Assert.All(row.Config.Values, static v => Assert.DoesNotContain(CliClientSecret, v, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_client_secret_env_name_is_read_from_serves_environment_for_an_agent()
    {
        var world = new ConnectionHarness();
        world.Environment.Set("GH_CLIENT_SECRET", CliClientSecret);
        var pending = world.Add().Handle(ConnectionHarness.Oauth() with { ClientSecretEnv = "GH_CLIENT_SECRET" }, Token).AsTask();
        world.SignIn.Succeed(new TokenSet("access-1", "refresh-1", ExpiresAt: null));

        _ = await pending;

        Assert.Equal(CliClientSecret, world.SignIn.LastClientSecret);
    }

    [Fact]
    public async Task A_failed_background_sign_in_is_what_get_answers_for_that_name_until_it_is_added_again()
    {
        var world = new ConnectionHarness();
        _ = await world.Add().Handle(ConnectionHarness.Oauth(wait: false), Token);

        world.SignIn.Fail(SindriErrors.ConnectionConsentDenied.With("denied"));
        Kernel.Result<GetConnectionResponse> denied = SindriErrors.ConnectionNotFound.With("not yet");
        for (var attempt = 0; attempt < 100 && denied.Error.Code == SindriErrors.ConnectionNotFound.Code; attempt++)
        {
            await Task.Delay(50, Token);
            denied = await world.Get().Handle(new GetConnectionRequest("linear:work"), Token);
        }

        Assert.Equal(SindriErrors.ConnectionConsentDenied.Code, denied.Error.Code);
        Assert.Contains(world.Logs.Lines, static l => l.Contains("SND-CONN-009", StringComparison.Ordinal));   // logged by code
        Assert.Empty(world.Store.Rows);
    }

    private static async Task<T> Eventually<T>(Func<T?> read)
        where T : class
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (read() is { } found)
            {
                return found;
            }

            await Task.Delay(50, Token);
        }

        throw new TimeoutException("the condition never held");
    }
}
