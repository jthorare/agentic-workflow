using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;
using Aw.Sindri.Features.TestConnection;
using Aw.Sindri.Testing.Connections;

namespace Aw.Sindri.Tests.Connections;

[Lane(TestLane.Unit)]
public sealed class TestConnectionServiceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<Connection> SeedRest(ConnectionHarness world, string? probeUrl)
    {
        var name = ConnectionSamples.Name("acme", "key");
        var pointer = world.Secrets.Store(name, new TokenSet("sk-live-ACME-0001", RefreshToken: null, ExpiresAt: null)).Value;
        var config = new Dictionary<string, string>(StringComparer.Ordinal);
        if (probeUrl is not null)
        {
            config["probeUrl"] = probeUrl;
        }

        var connection = ConnectionSamples.Oauth(name) with { Kind = ConnectionKind.Rest, Preset = null, SecretPointer = pointer, Config = config };
        _ = await world.Store.Upsert(connection, Token);
        return connection;
    }

    [Fact]
    public async Task A_rest_connection_test_calls_its_probe_url_with_the_stored_key()
    {
        var world = new ConnectionHarness();
        _ = await SeedRest(world, "https://api.acme.example.test/ping");

        var tested = await world.Test().Handle(new TestConnectionRequest("acme:key"), Token);

        Assert.True(tested.IsSuccess);
        Assert.True(tested.Value.Ok);
        Assert.Equal([("https://api.acme.example.test/ping", "sk-live-ACME-0001")], world.Probe.Calls);
        Assert.Equal("connected", tested.Value.Connection.Status);
    }

    [Fact]
    public async Task An_oauth_connection_test_uses_the_presets_probe_url()
    {
        var world = new ConnectionHarness();
        var pointer = world.Secrets.Store(ConnectionSamples.Name(), new TokenSet("access-1", "refresh-1", ExpiresAt: null)).Value;
        _ = await world.Store.Upsert(ConnectionSamples.Oauth() with { SecretPointer = pointer }, Token);

        var tested = await world.Test().Handle(new TestConnectionRequest("linear:work"), Token);

        Assert.True(tested.Value.Ok);
        Assert.Equal(ConnectionHarness.Linear.ProbeUrl, Assert.Single(world.Probe.Calls).Url);
    }

    [Fact]
    public async Task A_refused_credential_is_Ok_false_marks_the_connection_and_shows_no_token()
    {
        var world = new ConnectionHarness();
        _ = await SeedRest(world, "https://api.acme.example.test/ping");
        world.Probe.Refuses = true;

        var tested = await world.Test().Handle(new TestConnectionRequest("acme:key"), Token);

        Assert.True(tested.IsSuccess);
        Assert.False(tested.Value.Ok);
        Assert.NotNull(tested.Value.Reason);
        Assert.DoesNotContain("sk-live-ACME-0001", tested.Value.Reason, StringComparison.Ordinal);
        Assert.NotEqual(ConnectionStatus.Connected, Assert.Single(world.Store.Rows).Status);
    }

    [Fact]
    public async Task An_unknown_name_is_SND_CONN_001()
    {
        var world = new ConnectionHarness();

        var tested = await world.Test().Handle(new TestConnectionRequest("acme:nobody"), Token);

        Assert.Equal(SindriErrors.ConnectionNotFound.Code, tested.Error.Code);
    }
}
