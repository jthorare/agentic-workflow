using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Features.TestConnection;
using Aw.Sindri.Testing.Connections;

namespace Aw.Sindri.Tests.Connections;

/// <summary>#4361: testing a key connection that has a preset asks the same check the add ran (IKeyCheck), not only the probe URL.</summary>
[Lane(TestLane.Unit)]
public sealed class TestKeyConnectionTests
{
    private const string Key = "key-REVOKED-0001";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Slack's key preset: auth.test answers 200 with ok:false for a revoked token, so the probe alone passes it.</summary>
    private static ConnectionPreset Slack { get; } = ConnectionHarness.Linear with
    {
        Key = "slack",
        DisplayName = "Slack",
        HostGroup = "slack",
        ProbeUrl = "https://slack.example.test/api/auth.test",
        Auth = [ConnectionPreset.KeyAuth, ConnectionPreset.OAuthAuth],
    };

    /// <summary>Linear's key preset with no probe URL: its rest row has nothing to probe.</summary>
    private static ConnectionPreset LinearKey { get; } = ConnectionHarness.Linear with { ProbeUrl = null, Auth = [ConnectionPreset.KeyAuth, ConnectionPreset.OAuthAuth] };

    /// <summary>A key preset with no key check and no probe URL: there is nothing to try the key against.</summary>
    private static ConnectionPreset Unchecked { get; } = ConnectionHarness.Linear with { Key = "acme", DisplayName = "Acme", HostGroup = "acme", ProbeUrl = null, Auth = [ConnectionPreset.KeyAuth] };

    // A rest row as the key add writes it: the preset's key, its probe URL in Config when it has one, the key in the secret store.
    private static async Task<ConnectionHarness> World(ConnectionPreset preset)
    {
        var world = new ConnectionHarness { Presets = new FakeConnectionPresets(Slack, LinearKey, Unchecked) };
        var name = ConnectionSamples.Name(preset.Key, "work");
        var pointer = world.Secrets.Store(name, new TokenSet(Key, RefreshToken: null, ExpiresAt: null)).Value;
        var config = new Dictionary<string, string>(StringComparer.Ordinal);
        if (preset.ProbeUrl is { } probe)
        {
            config["probeUrl"] = probe;
        }

        _ = await world.Store.Upsert(ConnectionSamples.Oauth(name) with { Kind = ConnectionKind.Rest, Preset = preset.Key, SecretPointer = pointer, Config = config }, Token);
        return world;
    }

    [Fact]
    public async Task A_revoked_Slack_key_tests_as_not_connected_even_when_auth_test_answers_200()
    {
        var world = await World(Slack);
        world.Keys.Refuses = true;   // auth.test said ok:false; the probe (status only) still accepts

        var tested = await world.Test().Handle(new TestConnectionRequest("slack:work"), Token);

        Assert.True(tested.IsSuccess);
        Assert.False(tested.Value.Ok);
        Assert.EndsWith("(SND-CONN-018)", tested.Value.Reason, StringComparison.Ordinal);
        Assert.Equal(ConnectionStatus.Error, Assert.Single(world.Store.Rows).Status);
        Assert.Equal([("slack", Key)], world.Keys.Calls);
    }

    [Fact]
    public async Task A_revoked_Linear_key_with_no_probe_url_is_asked_of_Linear_and_tests_as_not_connected()
    {
        var world = await World(LinearKey);
        world.Keys.Refuses = true;

        var tested = await world.Test().Handle(new TestConnectionRequest("linear:work"), Token);

        Assert.False(tested.Value.Ok);
        Assert.EndsWith("(SND-CONN-018)", tested.Value.Reason, StringComparison.Ordinal);
        Assert.Equal(ConnectionStatus.Error, Assert.Single(world.Store.Rows).Status);
        Assert.Equal([("linear", Key)], world.Keys.Calls);
    }

    [Fact]
    public async Task A_key_whose_service_cannot_be_reached_is_SND_CONN_019()
    {
        var world = await World(Slack);
        world.Keys.Unreachable = true;

        var tested = await world.Test().Handle(new TestConnectionRequest("slack:work"), Token);

        Assert.False(tested.Value.Ok);
        Assert.EndsWith("(SND-CONN-019)", tested.Value.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_service_out_of_reach_leaves_the_row_as_it_was()
    {
        var world = await World(Slack);
        var before = Assert.Single(world.Store.Rows);
        world.Keys.Unreachable = true;

        var tested = await world.Test().Handle(new TestConnectionRequest("slack:work"), Token);

        Assert.False(tested.Value.Ok);
        Assert.Equal(before, Assert.Single(world.Store.Rows));
        Assert.Equal("connected", tested.Value.Connection.Status);
    }

    [Fact]
    public async Task A_rate_limited_check_is_SND_CONN_020_and_leaves_the_row_as_it_was()
    {
        var world = await World(Slack);
        var before = Assert.Single(world.Store.Rows);
        world.Keys.RateLimited = true;

        var tested = await world.Test().Handle(new TestConnectionRequest("slack:work"), Token);

        Assert.False(tested.Value.Ok);
        Assert.Equal("The service is rate limiting Sindri; the key was not checked (SND-CONN-020)", tested.Value.Reason);
        Assert.Equal(before, Assert.Single(world.Store.Rows));
    }

    [Fact]
    public async Task A_rest_connection_whose_preset_has_no_check_tries_the_probe_url_in_its_config()
    {
        var world = await World(Unchecked);
        var row = Assert.Single(world.Store.Rows);
        var config = new Dictionary<string, string>(StringComparer.Ordinal) { ["probeUrl"] = "https://acme.example.test/me" };
        _ = await world.Store.Upsert(row with { Config = config }, Token);

        var tested = await world.Test().Handle(new TestConnectionRequest("acme:work"), Token);

        Assert.True(tested.Value.Ok);
        Assert.Null(tested.Value.Reason);
        Assert.Equal([("https://acme.example.test/me", Key)], world.Probe.Calls);
        Assert.Empty(world.Keys.Calls);
    }

    [Fact]
    public async Task An_empty_stored_key_tests_as_not_connected_without_a_call()
    {
        var world = await World(LinearKey);
        var row = Assert.Single(world.Store.Rows);
        _ = world.Secrets.Store(row.Name, new TokenSet(string.Empty, RefreshToken: null, ExpiresAt: null));

        var tested = await world.Test().Handle(new TestConnectionRequest("linear:work"), Token);

        Assert.True(tested.IsSuccess);
        Assert.False(tested.Value.Ok);
        Assert.EndsWith("(SND-CONN-018)", tested.Value.Reason, StringComparison.Ordinal);
        Assert.Empty(world.Keys.Calls);
    }

    [Fact]
    public async Task A_good_Linear_key_is_checked_once_and_tests_as_connected()
    {
        var world = await World(LinearKey);

        var tested = await world.Test().Handle(new TestConnectionRequest("linear:work"), Token);

        Assert.True(tested.Value.Ok);
        Assert.Null(tested.Value.Reason);
        Assert.Equal("connected", tested.Value.Connection.Status);
        _ = Assert.Single(world.Keys.Calls);
        Assert.Empty(world.Probe.Calls);
    }

    [Fact]
    public async Task A_key_preset_with_no_check_and_no_probe_url_says_it_tried_nothing()
    {
        var world = await World(Unchecked);

        var tested = await world.Test().Handle(new TestConnectionRequest("acme:work"), Token);

        Assert.True(tested.Value.Ok);
        Assert.Equal("the stored secret was read; this connection has no probe URL to try it against", tested.Value.Reason);
        Assert.Empty(world.Keys.Calls);
        Assert.Empty(world.Probe.Calls);
    }
}
