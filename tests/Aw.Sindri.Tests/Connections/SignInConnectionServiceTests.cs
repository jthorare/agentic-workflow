using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.AddConnection;
using Aw.Sindri.Features.GetConnection;
using Aw.Sindri.Features.RemoveConnection;
using Aw.Sindri.Testing.Connections;

namespace Aw.Sindri.Tests.Connections;

/// <summary>'connection signin': the stored row signs in again without a remove and re-add (connections PR 10b).</summary>
[Lane(TestLane.Unit)]
public sealed class SignInConnectionServiceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<(ConnectionHarness World, Connection Row)> NeedingSignIn()
    {
        var world = new ConnectionHarness();
        await world.Hold();
        var row = ConnectionSamples.Oauth(status: ConnectionStatus.NeedsSignIn);
        _ = world.Secrets.Store(row.Name, new TokenSet("old-access", "old-refresh", ExpiresAt: null, ClientSecret: "cs-stored"));
        _ = await world.Store.Upsert(row, Token);
        return (world, row);
    }

    [Fact]
    public async Task SignIn_again_keeps_the_row_uses_the_stored_client_and_ends_connected_with_the_new_tokens()
    {
        var (world, row) = await NeedingSignIn();
        world.Clock.Advance(TimeSpan.FromHours(1));
        world.SignIn.Succeed(new TokenSet("new-access", "new-refresh", ExpiresAt: null));

        var signed = await world.SignInAgain().Handle(new SignInConnectionRequest("linear:work", Wait: true), Token);

        Assert.True(signed.IsSuccess);
        Assert.Equal(["client-123"], world.SignIn.Clients);
        Assert.Equal("cs-stored", world.SignIn.LastClientSecret);
        Assert.Equal(["read", "write"], world.SignIn.LastScopes);
        var stored = Assert.Single(world.Store.Rows);
        Assert.Equal((ConnectionStatus.Connected, null, row.CreatedAt, row.CreatedBy), (stored.Status, stored.StatusReason, stored.CreatedAt, stored.CreatedBy));
        Assert.Equal(world.Clock.GetUtcNow(), stored.LastRefreshedAt);
        Assert.Equal("new-access", world.Secrets.Tokens(stored.SecretPointer!.ToString())!.AccessToken);
        Assert.Equal("cs-stored", world.Secrets.Tokens(stored.SecretPointer.ToString())!.ClientSecret);
        Assert.Contains(EventTypes.ConnectionSignedIn, world.Ledger.Events.Select(static e => e.Envelope.Type), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Without_waiting_the_consent_url_comes_back_at_once_and_the_row_keeps_its_old_state_until_the_callback()
    {
        var (world, _) = await NeedingSignIn();

        var signed = await world.SignInAgain().Handle(new SignInConnectionRequest("linear:work"), Token);

        Assert.True(signed.IsSuccess);
        Assert.Equal(world.SignIn.ConsentUrl.ToString(), signed.Value.ConsentUrl);
        Assert.Equal(ConnectionStatus.NeedsSignIn, Assert.Single(world.Store.Rows).Status);
    }

    [Fact]
    public async Task A_key_based_connection_is_SND_CONN_011()
    {
        var world = new ConnectionHarness();
        _ = await world.Store.Upsert(ConnectionSamples.Oauth() with { Kind = ConnectionKind.Rest, Preset = null }, Token);

        var signed = await world.SignInAgain().Handle(new SignInConnectionRequest("linear:work", Wait: true), Token);

        Assert.Equal(SindriErrors.ConnectionKindNoSignIn.Code, signed.Error.Code);
        Assert.Contains("connection remove", signed.Error.Message, StringComparison.Ordinal);
        Assert.Empty(world.SignIn.Clients);
    }

    [Fact]
    public async Task An_unknown_name_is_SND_CONN_001()
    {
        var world = new ConnectionHarness();

        var signed = await world.SignInAgain().Handle(new SignInConnectionRequest("linear:nobody", Wait: true), Token);

        Assert.Equal(SindriErrors.ConnectionNotFound.Code, signed.Error.Code);
    }

    [Fact]
    public async Task An_mcp_connection_signs_in_through_the_server_url_it_stored()
    {
        var world = new ConnectionHarness();
        var row = ConnectionSamples.Oauth(ConnectionSamples.Name("acme", "mcp"), ConnectionStatus.NeedsSignIn) with
        {
            Kind = ConnectionKind.Mcp,
            Preset = null,
            Config = new Dictionary<string, string>(StringComparer.Ordinal) { ["serverUrl"] = "https://mcp.example.test/sse" },
        };
        _ = world.Secrets.Store(row.Name, new TokenSet("old", "old-refresh", ExpiresAt: null));
        _ = await world.Store.Upsert(row, Token);
        world.SignIn.Succeed(new TokenSet("new-access", "new-refresh", ExpiresAt: null));

        var signed = await world.SignInAgain().Handle(new SignInConnectionRequest("acme:mcp", Wait: true), Token);

        Assert.True(signed.IsSuccess);
        Assert.Equal([new Uri("https://mcp.example.test/sse")], world.SignIn.McpServers);
        Assert.Equal(ConnectionStatus.Connected, Assert.Single(world.Store.Rows).Status);
    }

    [Fact]
    public async Task A_denied_sign_in_leaves_the_row_and_the_old_secret_and_get_reports_the_failure()
    {
        var (world, row) = await NeedingSignIn();
        world.SignIn.Fail(SindriErrors.ConnectionConsentDenied.With("denied"));

        var signed = await world.SignInAgain().Handle(new SignInConnectionRequest("linear:work", Wait: true), Token);

        Assert.Equal(SindriErrors.ConnectionConsentDenied.Code, signed.Error.Code);
        Assert.Equal(row, Assert.Single(world.Store.Rows));
        Assert.Empty(world.Secrets.Deleted);
        Assert.Equal("old-access", world.Secrets.Tokens(row.SecretPointer!.ToString())!.AccessToken);
        var got = await world.Get().Handle(new GetConnectionRequest("linear:work"), Token);   // the row exists, so Get still says why the last try failed
        Assert.Equal(SindriErrors.ConnectionConsentDenied.Code, got.Error.Code);
    }

    [Fact]
    public async Task A_failed_sign_in_again_on_a_connected_row_keeps_the_row_working_and_get_reports_the_failure_beside_it()
    {
        var world = new ConnectionHarness();
        await world.Hold();
        var row = ConnectionSamples.Oauth(status: ConnectionStatus.Connected);
        _ = world.Secrets.Store(row.Name, new TokenSet("old-access", "old-refresh", ExpiresAt: null, ClientSecret: "cs-stored"));
        _ = await world.Store.Upsert(row, Token);
        world.SignIn.Fail(SindriErrors.ConnectionConsentDenied.With("denied"));

        var signed = await world.SignInAgain().Handle(new SignInConnectionRequest("linear:work", Wait: true), Token);
        var got = await world.Get().Handle(new GetConnectionRequest("linear:work"), Token);

        Assert.Equal(SindriErrors.ConnectionConsentDenied.Code, signed.Error.Code);
        Assert.True(got.IsSuccess);   // the row still works on its old tokens
        Assert.Equal("connected", got.Value.Connection.Status);
        Assert.Equal(new SignInFailureView(SindriErrors.ConnectionConsentDenied.Code, world.Clock.GetUtcNow()), got.Value.LastSignInFailure);
    }

    [Fact]
    public async Task A_stale_sign_in_try_that_fails_after_a_newer_one_started_leaves_no_failure_beside_a_connected_row()
    {
        var world = new ConnectionHarness();
        await world.Hold();
        var row = ConnectionSamples.Oauth(status: ConnectionStatus.Connected);
        _ = world.Secrets.Store(row.Name, new TokenSet("old-access", "old-refresh", ExpiresAt: null, ClientSecret: "cs-stored"));
        _ = await world.Store.Upsert(row, Token);
        var a = new FakeSignInSession();
        var b = new FakeSignInSession();
        _ = await world.SignInAgain(a).Handle(new SignInConnectionRequest("linear:work"), Token);
        _ = await world.SignInAgain(b).Handle(new SignInConnectionRequest("linear:work"), Token);

        a.Fail(SindriErrors.ConnectionSignInTimeout.With("timed out"));
        for (var i = 0; i < 100 && world.Logs.Lines.Count == 0; i++)
        {
            await Task.Delay(20, Token);
        }
        _ = Assert.Single(world.Logs.Lines);   // the stale try's failure is still logged
        var got = await world.Get().Handle(new GetConnectionRequest("linear:work"), Token);

        Assert.True(got.IsSuccess);
        Assert.Null(got.Value.LastSignInFailure);
    }

    [Fact]
    public async Task A_stale_sign_in_try_that_fails_after_a_newer_one_started_does_not_end_the_newer_try_on_a_row_that_is_not_connected()
    {
        var world = new ConnectionHarness();
        await world.Hold();
        var row = ConnectionSamples.Oauth(status: ConnectionStatus.NeedsSignIn);
        _ = world.Secrets.Store(row.Name, new TokenSet("old-access", "old-refresh", ExpiresAt: null, ClientSecret: "cs-stored"));
        _ = await world.Store.Upsert(row, Token);
        var a = new FakeSignInSession();
        var b = new FakeSignInSession();
        _ = await world.SignInAgain(a).Handle(new SignInConnectionRequest("linear:work"), Token);
        _ = await world.SignInAgain(b).Handle(new SignInConnectionRequest("linear:work"), Token);

        a.Fail(SindriErrors.ConnectionSignInTimeout.With("timed out"));
        for (var i = 0; i < 100 && world.Logs.Lines.Count == 0; i++)
        {
            await Task.Delay(20, Token);
        }
        _ = Assert.Single(world.Logs.Lines);   // the stale try's failure is still logged
        var got = await world.Get().Handle(new GetConnectionRequest("linear:work"), Token);

        Assert.True(got.IsSuccess);   // the row, not the stale try's SND-CONN-003
        Assert.Equal("needs-sign-in", got.Value.Connection.Status);
    }

    [Fact]
    public async Task A_connected_row_whose_last_sign_in_worked_reports_no_failure()
    {
        var world = new ConnectionHarness();
        await world.Hold();
        var row = ConnectionSamples.Oauth(status: ConnectionStatus.Connected);
        _ = world.Secrets.Store(row.Name, new TokenSet("old-access", "old-refresh", ExpiresAt: null, ClientSecret: "cs-stored"));
        _ = await world.Store.Upsert(row, Token);
        world.SignIn.Fail(SindriErrors.ConnectionConsentDenied.With("denied"));
        _ = await world.SignInAgain().Handle(new SignInConnectionRequest("linear:work", Wait: true), Token);
        var second = new FakeSignInSession();   // a sign-in session ends once: the next try is a new one
        second.Succeed(new TokenSet("new-access", "new-refresh", ExpiresAt: null));

        _ = await world.SignInAgain(second).Handle(new SignInConnectionRequest("linear:work", Wait: true), Token);
        var got = await world.Get().Handle(new GetConnectionRequest("linear:work"), Token);

        Assert.Null(got.Value.LastSignInFailure);
    }

    [Fact]
    public async Task Removing_a_connection_that_needs_a_sign_in_clears_its_needs_you_row()
    {
        var (world, _) = await NeedingSignIn();

        var removed = await world.Remove().Handle(new RemoveConnectionRequest("linear:work"), Token);

        Assert.True(removed.IsSuccess);
        Assert.Contains(EventTypes.ConnectionSignedIn, world.Ledger.Events.Select(static e => e.Envelope.Type), StringComparer.Ordinal);
    }
}
