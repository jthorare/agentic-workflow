using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Connections;

namespace Aw.Sindri.Tests.Connections;

[Lane(TestLane.Unit)]
public sealed class AddConnectionServiceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_launched_session_adding_a_connection_is_the_rows_creator()
    {
        var world = new ConnectionHarness();
        world.Environment.Set("ACME_KEY", "sk-live-ACME-0001");
        var caller = A.Caller().Launched("LCH-0000000000000000000000000000ff01").Build();
        using var entered = world.Callers.Enter(caller);

        var added = await world.Add().Handle(ConnectionHarness.Rest(), Token);

        Assert.True(added.IsSuccess);
        var row = Assert.Single(world.Store.Rows);
        Assert.Equal(caller.Actor, row.CreatedBy);
        Assert.Contains("LCH-0000000000000000000000000000ff01", row.CreatedBy, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generic_connection_add_refuses_the_messages_name()
    {
        var world = new ConnectionHarness();
        world.Environment.Set("ACME_KEY", "sk-live-ACME-0001");

        var added = await world.Add().Handle(ConnectionHarness.Rest("slack:messages"), Token);

        Assert.Equal(SindriErrors.ConnectionNameReserved.Code, added.Error.Code);
        Assert.Contains("sindri messages connect", added.Error.Message, StringComparison.Ordinal);
        Assert.Empty(world.Store.Rows);
        Assert.Empty(world.Secrets.Pointers);
    }

    [Fact]
    public async Task An_oauth_add_without_waiting_returns_the_consent_url_at_once_and_writes_no_row_yet()
    {
        var world = new ConnectionHarness();

        var added = await world.Add().Handle(ConnectionHarness.Oauth(wait: false), Token);

        Assert.True(added.IsSuccess);
        Assert.Equal(world.SignIn.ConsentUrl.ToString(), added.Value.ConsentUrl);
        Assert.Equal("needs-sign-in", added.Value.Connection.Status);
        Assert.False(added.Value.Connection.HasSecret);
        Assert.Equal("client-123", Assert.Single(world.SignIn.Clients));
        Assert.Empty(world.Store.Rows);
        Assert.Empty(world.Secrets.Pointers);
    }

    [Fact]
    public async Task The_background_sign_in_stores_the_tokens_and_the_row_when_the_callback_lands()
    {
        var world = new ConnectionHarness();
        _ = await world.Add().Handle(ConnectionHarness.Oauth(wait: false), Token);

        world.SignIn.Succeed(new TokenSet("access-1", "refresh-1", world.Clock.GetUtcNow().AddHours(1)));
        var row = await Eventually(() => world.Store.Rows is [var first, ..] ? first : null);

        Assert.Equal(ConnectionStatus.Connected, row.Status);
        Assert.Equal("linear:work", row.Name.ToString());
        Assert.Equal("access-1", world.Secrets.Tokens(row.SecretPointer!.ToString())!.AccessToken);
    }

    [Fact]
    public async Task A_waiting_add_returns_the_connected_connection_after_the_callback()
    {
        var world = new ConnectionHarness();
        var pending = world.Add().Handle(ConnectionHarness.Oauth(), Token).AsTask();
        world.SignIn.Succeed(new TokenSet("access-1", "refresh-1", ExpiresAt: null));

        var added = await pending;

        Assert.True(added.IsSuccess);
        Assert.Equal("connected", added.Value.Connection.Status);
        Assert.True(added.Value.Connection.HasSecret);
        Assert.Equal("client-123", added.Value.Connection.Config["clientId"]);
        var row = Assert.Single(world.Store.Rows);
        Assert.Equal(world.Clock.GetUtcNow(), row.CreatedAt);
        Assert.Equal("linear", row.Preset);
    }

    [Fact]
    public async Task A_preset_without_a_client_id_is_SND_CONN_007_and_starts_no_sign_in()
    {
        var world = new ConnectionHarness();

        var added = await world.Add().Handle(ConnectionHarness.Oauth(client: null), Token);

        Assert.True(added.IsError);
        Assert.Equal(SindriErrors.ConnectionNeedsClientId.Code, added.Error.Code);
        Assert.Empty(world.SignIn.Clients);
        Assert.Empty(world.Store.Rows);
    }

    [Fact]
    public async Task A_name_that_exists_is_SND_CONN_010_and_the_old_row_and_secret_stay()
    {
        var world = new ConnectionHarness();
        var existing = ConnectionSamples.Oauth();
        _ = await world.Store.Upsert(existing, Token);

        var added = await world.Add().Handle(ConnectionHarness.Oauth(), Token);

        Assert.Equal(SindriErrors.ConnectionExists.Code, added.Error.Code);
        Assert.Equal(existing, Assert.Single(world.Store.Rows));
        Assert.Empty(world.SignIn.Clients);
    }

    [Fact]
    public async Task A_bad_name_is_SND_CONN_008()
    {
        var world = new ConnectionHarness();

        var added = await world.Add().Handle(ConnectionHarness.Oauth(name: "Linear Work"), Token);

        Assert.Equal(SindriErrors.ConnectionNameInvalid.Code, added.Error.Code);
    }

    [Fact]
    public async Task An_unknown_preset_is_an_error_that_names_the_shipped_ones()
    {
        var world = new ConnectionHarness();

        var added = await world.Add().Handle(ConnectionHarness.Oauth() with { Preset = "myspace" }, Token);

        Assert.True(added.IsError);
        Assert.Contains("linear", added.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failed_sign_in_leaves_no_row_and_no_secret()
    {
        var world = new ConnectionHarness();
        var pending = world.Add().Handle(ConnectionHarness.Oauth(), Token).AsTask();
        world.SignIn.Fail(SindriErrors.ConnectionConsentDenied.With("denied"));

        var added = await pending;

        Assert.Equal(SindriErrors.ConnectionConsentDenied.Code, added.Error.Code);
        Assert.Empty(world.Store.Rows);
        Assert.Empty(world.Secrets.Pointers);
    }

    [Fact]
    public async Task A_rest_add_with_key_env_reads_the_variable_once_and_stores_it_as_the_secret()
    {
        var world = new ConnectionHarness();
        world.Environment.Set("ACME_KEY", "sk-live-ACME-0001");

        var added = await world.Add().Handle(ConnectionHarness.Rest(), Token);

        Assert.True(added.IsSuccess);
        Assert.Null(added.Value.ConsentUrl);
        Assert.Equal("connected", added.Value.Connection.Status);
        var row = Assert.Single(world.Store.Rows);
        Assert.Equal(ConnectionKind.Rest, row.Kind);
        Assert.Equal("sk-live-ACME-0001", world.Secrets.Tokens(row.SecretPointer!.ToString())!.AccessToken);
        Assert.DoesNotContain("sk-live-ACME-0001", row.ToString(), StringComparison.Ordinal);
        Assert.All(row.Config.Values, static v => Assert.DoesNotContain("sk-live", v, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_missing_key_env_variable_is_a_clear_error_naming_it_and_stores_nothing()
    {
        var world = new ConnectionHarness();

        var added = await world.Add().Handle(ConnectionHarness.Rest(keyEnv: "NOT_SET_KEY"), Token);

        Assert.True(added.IsError);
        Assert.Contains("NOT_SET_KEY", added.Error.Message, StringComparison.Ordinal);
        Assert.Empty(world.Store.Rows);
        Assert.Empty(world.Secrets.Pointers);
    }

    [Fact]
    public async Task A_rest_add_without_key_env_is_an_error()
    {
        var world = new ConnectionHarness();

        var added = await world.Add().Handle(ConnectionHarness.Rest() with { KeyEnv = null }, Token);

        Assert.True(added.IsError);
        Assert.Empty(world.Store.Rows);
    }

    [Fact]
    public async Task A_secret_store_that_fails_leaves_no_row()
    {
        var world = new ConnectionHarness();
        world.Environment.Set("ACME_KEY", "sk-live-ACME-0001");
        world.Secrets.Fails = true;

        var added = await world.Add().Handle(ConnectionHarness.Rest(), Token);

        Assert.Equal(SindriErrors.ConnectionSecretUnavailable.Code, added.Error.Code);
        Assert.DoesNotContain("sk-live-ACME-0001", added.Error.Message, StringComparison.Ordinal);
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

        throw new TimeoutException("the background sign-in never finished");
    }
}
