using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Testing.Connections;

namespace Aw.Sindri.Tests.Connections;

/// <summary>The 'connection-refresh' service (connections PR 10b), over the in-memory ports, the scripted token client and the harness clock.</summary>
[Lane(TestLane.Unit)]
public sealed class ConnectionRefreshTests
{
    private const string OldAccess = "access-OLD-0001";
    private const string OldRefresh = "refresh-OLD-0001";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<Connection> Seed(ConnectionHarness world, TimeSpan expiresIn, ConnectionKind kind = ConnectionKind.OAuth, ConnectionStatus status = ConnectionStatus.Connected)
    {
        var name = ConnectionSamples.Name();
        var pointer = world.Secrets.Store(name, new TokenSet(OldAccess, OldRefresh, world.Clock.GetUtcNow() + expiresIn)).Value;
        var connection = ConnectionSamples.Oauth(name, status) with { Kind = kind, SecretPointer = pointer };
        _ = await world.Store.Upsert(connection, Token);
        return connection;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_token_expiring_within_ten_minutes_is_refreshed_and_the_secret_replaced(bool providerRotates)
    {
        var world = new ConnectionHarness();
        var seeded = await Seed(world, TimeSpan.FromMinutes(9));
        var renewed = world.Clock.GetUtcNow().AddHours(1);
        world.OAuth.Next = new TokenSet("access-NEW-0002", providerRotates ? "refresh-NEW-0002" : null, renewed);

        var ran = await world.Refresh().Run(await world.Tick(), Token);

        Assert.True(ran.IsSuccess);
        Assert.False(ran.Value.Idle);
        var call = Assert.Single(world.OAuth.Refreshes);
        Assert.Equal(("linear", "client-123", OldRefresh), call);
        var stored = world.Secrets.Tokens(seeded.SecretPointer!.ToString())!;
        Assert.Equal("access-NEW-0002", stored.AccessToken);
        Assert.Equal(providerRotates ? "refresh-NEW-0002" : OldRefresh, stored.RefreshToken);   // a rotated refresh token is kept; with none returned the old one stays
        Assert.Equal(renewed, stored.ExpiresAt);
        var row = Assert.Single(world.Store.Rows);
        Assert.Equal(ConnectionStatus.Connected, row.Status);
        Assert.Equal(world.Clock.GetUtcNow(), row.LastRefreshedAt);
        Assert.Empty(world.Ledger.Events);
    }

    [Fact]
    public async Task A_token_expiring_later_is_left_alone()
    {
        var world = new ConnectionHarness();
        var seeded = await Seed(world, TimeSpan.FromMinutes(11));

        var ran = await world.Refresh().Run(await world.Tick(), Token);

        Assert.True(ran.IsSuccess);
        Assert.True(ran.Value.Idle);
        Assert.Empty(world.OAuth.Refreshes);
        Assert.Equal(OldAccess, world.Secrets.Tokens(seeded.SecretPointer!.ToString())!.AccessToken);
        Assert.Null(Assert.Single(world.Store.Rows).LastRefreshedAt);
    }

    [Fact]
    public async Task A_failed_refresh_marks_needs_sign_in_and_adds_a_Needs_you_row()
    {
        var world = new ConnectionHarness();
        _ = await Seed(world, TimeSpan.FromMinutes(2));
        world.OAuth.RefuseWith = "invalid_grant";
        var service = world.Refresh();
        var tick = await world.Tick();

        var ran = await service.Run(tick, Token);
        var again = await service.Run(tick, Token);

        Assert.True(ran.IsSuccess);
        Assert.True(again.IsSuccess);
        var row = Assert.Single(world.Store.Rows);
        Assert.Equal(ConnectionStatus.NeedsSignIn, row.Status);
        Assert.Contains("invalid_grant", row.StatusReason, StringComparison.Ordinal);   // the provider's error code, never a token
        Assert.DoesNotContain(OldRefresh, row.StatusReason, StringComparison.Ordinal);
        var recorded = Assert.Single(world.Ledger.Events);   // ONE event: the second tick skips a connection that already needs a sign-in
        Assert.Equal(EventTypes.ConnectionNeedsSignIn, recorded.Envelope.Type);
        using var payload = JsonDocument.Parse(recorded.Envelope.Payload);
        Assert.Equal("linear:work", payload.RootElement.GetProperty("connection").GetString());
        Assert.Contains("invalid_grant", payload.RootElement.GetProperty("reason").GetString(), StringComparison.Ordinal);
        _ = Assert.Single(world.OAuth.Refreshes);
        Assert.DoesNotContain(OldAccess, recorded.Envelope.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain(OldRefresh, recorded.Envelope.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_connection_signed_in_again_clears_its_Needs_you_row()
    {
        var world = new ConnectionHarness();
        await world.Hold();
        _ = await Seed(world, TimeSpan.FromMinutes(2));
        world.OAuth.RefuseWith = "invalid_grant";
        _ = await world.Refresh().Run(await world.Tick(), Token);
        Assert.Equal(EventTypes.ConnectionNeedsSignIn, Assert.Single(world.Ledger.Events).Envelope.Type);

        var tested = await world.Test().Handle(new Features.TestConnection.TestConnectionRequest("linear:work"), Token);   // the probe accepts the stored token this time

        Assert.True(tested.Value.Ok);
        Assert.Equal(ConnectionStatus.Connected, Assert.Single(world.Store.Rows).Status);
        Assert.Equal([EventTypes.ConnectionNeedsSignIn, EventTypes.ConnectionSignedIn], world.Ledger.Events.Select(static e => e.Envelope.Type), StringComparer.Ordinal);   // 'connection.signed-in' removes the row 'connection:linear:work'
    }

    [Fact]
    public async Task Only_oauth_connections_are_refreshed()
    {
        var world = new ConnectionHarness();
        _ = await Seed(world, TimeSpan.FromMinutes(1), ConnectionKind.Rest);

        var ran = await world.Refresh().Run(await world.Tick(), Token);

        Assert.True(ran.Value.Idle);
        Assert.Empty(world.OAuth.Refreshes);
        Assert.Equal(ConnectionStatus.Connected, Assert.Single(world.Store.Rows).Status);
    }
}
