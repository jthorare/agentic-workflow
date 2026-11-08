using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.AddConnection;
using Aw.Sindri.Features.TestConnection;
using Aw.Sindri.Testing.Connections;

namespace Aw.Sindri.Tests.Connections;

/// <summary>
/// The review of #2368: only the provider's refusal moves a row to needs-sign-in, a write made after a slow call never overwrites a newer sign-in or refresh,
/// a removal during the call leaves no secret and no Needs-you row, one connection's error does not stop the others, and every move into needs-sign-in adds its row once.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ConnectionRefreshSafetyTests
{
    private const string OldAccess = "access-OLD-0001";
    private const string OldRefresh = "refresh-OLD-0001";
    private const string Canary = "aw-canary-secret-91c4e7";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<Connection> Seed(ConnectionHarness world, string label = "work", string? clientSecret = null)
    {
        var name = ConnectionSamples.Name("linear", label);
        var pointer = world.Secrets.Store(name, new TokenSet(OldAccess, OldRefresh, world.Clock.GetUtcNow().AddMinutes(2), clientSecret)).Value;
        var connection = ConnectionSamples.Oauth(name) with { SecretPointer = pointer };
        _ = await world.Store.Upsert(connection, Token);
        return connection;
    }

    [Fact]
    public async Task A_provider_that_cannot_be_reached_leaves_the_row_connected_and_the_next_tick_retries()
    {
        var world = new ConnectionHarness();
        _ = await Seed(world);
        world.OAuth.Unreachable = true;
        var service = world.Refresh();

        var first = await service.Run(await world.Tick(), Token);
        world.OAuth.Unreachable = false;
        world.OAuth.Next = new TokenSet("access-NEW-0002", "refresh-NEW-0002", world.Clock.GetUtcNow().AddHours(1));
        var second = await service.Run(await world.Tick(), Token);

        Assert.True(first.IsSuccess);
        Assert.True(first.Value.Idle);
        Assert.Equal(2, world.OAuth.Refreshes.Count);
        var row = Assert.Single(world.Store.Rows);
        Assert.Equal(ConnectionStatus.Connected, row.Status);
        Assert.Null(row.StatusReason);
        Assert.Empty(world.Ledger.Events);
        Assert.False(second.Value.Idle);
        Assert.True(row.LastRefreshedAt.HasValue);
    }

    [Fact]
    public async Task A_provider_that_answers_temporarily_unavailable_leaves_the_row_connected()
    {
        var world = new ConnectionHarness();
        _ = await Seed(world);
        world.OAuth.RefuseWith = "temporarily_unavailable";

        var ran = await world.Refresh().Run(await world.Tick(), Token);

        Assert.True(ran.Value.Idle);
        var row = Assert.Single(world.Store.Rows);
        Assert.Equal(ConnectionStatus.Connected, row.Status);
        Assert.Empty(world.Ledger.Events);
    }

    [Theory]
    [InlineData("invalid_grant")]
    [InlineData("invalid_client")]
    [InlineData("unauthorized_client")]
    public async Task An_auth_refusal_says_the_provider_refused_and_names_its_code(string code)
    {
        var world = new ConnectionHarness();
        _ = await Seed(world);
        world.OAuth.RefuseWith = code;

        _ = await world.Refresh().Run(await world.Tick(), Token);

        var row = Assert.Single(world.Store.Rows);
        Assert.Equal(ConnectionStatus.NeedsSignIn, row.Status);
        Assert.Equal($"the provider refused the refresh ({code})", row.StatusReason);
    }

    [Fact]
    public async Task A_sign_in_during_the_refresh_call_keeps_the_sign_ins_tokens()
    {
        var world = new ConnectionHarness();
        var seeded = await Seed(world);
        var signedInAt = world.Clock.GetUtcNow().AddSeconds(30);
        world.OAuth.Next = new TokenSet("access-REFRESHED-0002", "refresh-REFRESHED-0002", world.Clock.GetUtcNow().AddHours(1));
        world.OAuth.DuringRefresh = async () =>
        {
            _ = world.Secrets.Store(seeded.Name, new TokenSet("access-SIGNIN-0003", "refresh-SIGNIN-0003", signedInAt.AddHours(1)));   // the same keychain key, as the real sign-in writes it
            _ = await world.Store.Update(seeded with { UpdatedAt = signedInAt, LastRefreshedAt = signedInAt }, Token);
        };

        var ran = await world.Refresh().Run(await world.Tick(), Token);

        Assert.True(ran.IsSuccess);
        var stored = world.Secrets.Tokens(seeded.SecretPointer!.ToString())!;
        Assert.Equal("access-SIGNIN-0003", stored.AccessToken);
        Assert.Equal("refresh-SIGNIN-0003", stored.RefreshToken);
        var row = Assert.Single(world.Store.Rows);
        Assert.Equal(signedInAt, row.UpdatedAt);
        Assert.Equal(signedInAt, row.LastRefreshedAt);
    }

    [Fact]
    public async Task A_removal_during_the_refresh_call_stores_no_secret()
    {
        var world = new ConnectionHarness();
        var seeded = await Seed(world);
        world.OAuth.Next = new TokenSet("access-NEW-0002", "refresh-NEW-0002", world.Clock.GetUtcNow().AddHours(1));
        world.OAuth.DuringRefresh = async () =>
        {
            _ = await world.Store.Remove(seeded.Name, Token);
            _ = world.Secrets.Delete(seeded.SecretPointer!);
        };

        var ran = await world.Refresh().Run(await world.Tick(), Token);

        Assert.True(ran.IsSuccess);
        Assert.Empty(world.Secrets.Pointers);   // nothing was stored for a connection that is gone
        Assert.Empty(world.Store.Rows);
    }

    [Fact]
    public async Task A_removal_during_a_refused_refresh_adds_no_Needs_you_row_and_does_not_stop_the_run()
    {
        var world = new ConnectionHarness();
        var first = await Seed(world, "a-first");
        _ = await Seed(world, "b-second");
        world.OAuth.RefuseWith = "invalid_grant";
        var calls = 0;
        world.OAuth.DuringRefresh = async () =>
        {
            if (++calls == 1)
            {
                _ = await world.Store.Remove(first.Name, Token);
            }
        };

        var ran = await world.Refresh().Run(await world.Tick(), Token);

        Assert.True(ran.IsSuccess);
        Assert.Equal(2, world.OAuth.Refreshes.Count);   // the second connection still got its turn
        Assert.Equal("linear:b-second", Assert.Single(world.Store.Rows).Name.ToString());
        var recorded = Assert.Single(world.Ledger.Events);
        Assert.Contains("linear:b-second", recorded.Envelope.Payload, StringComparison.Ordinal);   // none for the removed one
    }

    [Fact]
    public async Task A_store_error_on_one_connection_is_logged_and_the_others_are_still_refreshed()
    {
        var world = new ConnectionHarness();
        _ = await Seed(world, "a-first");
        var second = await Seed(world, "b-second");
        world.OAuth.Next = new TokenSet("access-NEW-0002", "refresh-NEW-0002", world.Clock.GetUtcNow().AddHours(1));
        var failing = new FailingUpdateStore(world.Store, "linear:a-first");

        var ran = await world.RefreshOver(failing).Run(await world.Tick(), Token);

        Assert.True(ran.IsSuccess);
        Assert.False(ran.Value.Idle);
        Assert.Equal("access-NEW-0002", world.Secrets.Tokens(second.SecretPointer!.ToString())!.AccessToken);
        Assert.Contains(world.Logs.Lines, static l => l.Contains("linear:a-first", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_refresh_during_the_test_probe_is_not_stamped_needs_sign_in()
    {
        var world = new ConnectionHarness();
        var seeded = await Seed(world);
        var refreshedAt = world.Clock.GetUtcNow().AddSeconds(20);
        var probe = new RefreshingProbe(async () => await world.Store.Update(seeded with { LastRefreshedAt = refreshedAt, UpdatedAt = refreshedAt }, Token));

        var tested = await world.TestWith(probe).Handle(new TestConnectionRequest("linear:work"), Token);

        Assert.True(tested.IsSuccess);
        var row = Assert.Single(world.Store.Rows);
        Assert.Equal(ConnectionStatus.Connected, row.Status);   // the 401 was the old token's; the refreshed row is not marked
        Assert.Equal(refreshedAt, row.UpdatedAt);
        Assert.Empty(world.Ledger.Events);
    }

    [Fact]
    public async Task A_test_that_needs_a_new_sign_in_adds_the_Needs_you_row_once()
    {
        var world = new ConnectionHarness();
        await world.Hold();
        _ = await Seed(world);
        world.Probe.Refuses = true;

        _ = await world.Test().Handle(new TestConnectionRequest("linear:work"), Token);
        _ = await world.Test().Handle(new TestConnectionRequest("linear:work"), Token);

        Assert.Equal(ConnectionStatus.NeedsSignIn, Assert.Single(world.Store.Rows).Status);
        Assert.Equal([EventTypes.ConnectionNeedsSignIn], world.Ledger.Events.Select(static e => e.Envelope.Type), StringComparer.Ordinal);   // the second failing test does not add another
    }

    [Fact]
    public async Task A_removal_during_the_sign_in_wait_leaves_no_secret()
    {
        var world = new ConnectionHarness();
        await world.Hold();
        var row = ConnectionSamples.Oauth(status: ConnectionStatus.NeedsSignIn);
        _ = world.Secrets.Store(row.Name, new TokenSet("old-access", "old-refresh", ExpiresAt: null));
        _ = await world.Store.Upsert(row, Token);
        var signing = world.SignInAgain().Handle(new SignInConnectionRequest("linear:work", Wait: true), Token).AsTask();

        _ = await world.Store.Remove(row.Name, Token);
        world.SignIn.Succeed(new TokenSet("new-access", "new-refresh", ExpiresAt: null));
        var signed = await signing;

        Assert.Equal(SindriErrors.ConnectionNotFound.Code, signed.Error.Code);
        Assert.Empty(world.Secrets.Pointers);   // the tokens the sign-in just stored are deleted with no row to own them
    }

    [Fact]
    public async Task A_sign_in_stamps_the_time_only_a_sign_in_changes()
    {
        var world = new ConnectionHarness();
        await world.Hold();
        var row = ConnectionSamples.Oauth(status: ConnectionStatus.NeedsSignIn);
        _ = world.Secrets.Store(row.Name, new TokenSet("old-access", "old-refresh", ExpiresAt: null));
        _ = await world.Store.Upsert(row, Token);
        world.SignIn.Succeed(new TokenSet("new-access", "new-refresh", ExpiresAt: null));

        _ = await world.SignInAgain().Handle(new SignInConnectionRequest("linear:work", Wait: true), Token);

        Assert.Equal(world.Clock.GetUtcNow().ToString("O", System.Globalization.CultureInfo.InvariantCulture), Assert.Single(world.Store.Rows).Config["signedInAt"]);
    }

    [Fact]
    public async Task A_rotated_refresh_token_and_the_client_secret_reach_no_row_event_or_log()
    {
        var world = new ConnectionHarness();
        _ = await Seed(world, clientSecret: Canary + "-client");
        world.OAuth.Next = new TokenSet(Canary + "-access", Canary + "-refresh", world.Clock.GetUtcNow().AddHours(1));
        _ = await world.Refresh().Run(await world.Tick(), Token);
        world.OAuth.Unreachable = true;
        world.OAuth.ErrorCanary = Canary + "-provider-text";
        world.Clock.Advance(TimeSpan.FromMinutes(59));
        _ = await world.Refresh().Run(await world.Tick(), Token);

        var seen = string.Join('\n', world.Store.Rows.Select(static r => $"{r.StatusReason} {string.Join(',', r.Config.Values)} {r.SecretPointer}")) + string.Join('\n', world.Logs.Lines) + string.Join('\n', world.Ledger.Events.Select(static e => e.Envelope.Payload));
        Assert.DoesNotContain(Canary, seen, StringComparison.Ordinal);
        Assert.Equal(Canary + "-refresh", world.Secrets.Tokens(Assert.Single(world.Store.Rows).SecretPointer!.ToString())!.RefreshToken);   // it is stored, just never shown
    }

    // A probe that lets a refresh land while it is in flight, then is refused.
    private sealed class RefreshingProbe(Func<ValueTask<Result<Unit>>> during) : IConnectionProbe
    {
        public async ValueTask<Result<Unit>> Probe(string url, TokenSet tokens, CancellationToken cancellationToken)
        {
            _ = await during().ConfigureAwait(false);
            return SindriErrors.ConnectionNeedsSignIn.With("the provider answered 401");
        }
    }

    // The in-memory store, except that Update of one name fails.
    private sealed class FailingUpdateStore(InMemoryConnectionStore inner, string name) : IConnectionStore
    {
        public ValueTask<Result<Connection>> Get(ConnectionName n, CancellationToken ct) => inner.Get(n, ct);

        public ValueTask<Result<IReadOnlyList<Connection>>> List(CancellationToken ct) => inner.List(ct);

        public ValueTask<Result<Unit>> Upsert(Connection c, CancellationToken ct) => inner.Upsert(c, ct);

        public ValueTask<Result<Unit>> Insert(Connection c, CancellationToken ct) => inner.Insert(c, ct);

        public ValueTask<Result<Unit>> Update(Connection c, CancellationToken ct)
            => string.Equals(c.Name.ToString(), name, StringComparison.Ordinal)
                ? ValueTask.FromResult<Result<Unit>>(SindriErrors.ConnectionSecretUnavailable.With("the database is locked"))
                : inner.Update(c, ct);

        public ValueTask<Result<bool>> Remove(ConnectionName n, CancellationToken ct) => inner.Remove(n, ct);
    }
}
