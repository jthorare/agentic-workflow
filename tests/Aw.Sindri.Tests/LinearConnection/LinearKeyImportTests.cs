using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Host.IndexSources;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Connections;

namespace Aw.Sindri.Tests.LinearConnection;

/// <summary>The one-time import of the old Linear keychain entry into the one Linear connection (#3364).</summary>
[Lane(TestLane.Unit)]
public sealed class LinearKeyImportTests
{
    private const string Pointer = "keychain:sindri-index/linear";

    private readonly ConnectionHarness _world = new();
    private readonly FakeSecretWriter _keychain = new();
    private readonly InMemoryProfiles _profiles = new();

    public LinearKeyImportTests()
    {
        var profile = new SindriProfile(1, "thor", Context: JsonDocument.Parse("{\"sources\":{\"linear\":{\"teams\":[\"ENG\"],\"token\":\"" + Pointer + "\"}}}").RootElement.Clone());
        _profiles.Live = new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml");
        _ = _keychain.Store("sindri-index", "linear", "lin_api_old");
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private LinearKeyImport Import() => new(_profiles, _keychain, _world.Store, _world.Secrets, _world.Presets, _world.Events(), _world.Clock);

    [Fact]
    public async Task The_old_keychain_entry_and_no_connection_make_one_connection_and_one_event()
    {
        await _world.Hold();

        var imported = await Import().Run(Token);

        Assert.True(imported.Value);
        var row = Assert.Single(_world.Store.Rows);
        Expect.Equal("linear:default", row.Name.ToString());
        Expect.Equal(ConnectionStatus.Connected, row.Status);
        Expect.Equal("lin_api_old", _world.Secrets.Tokens(row.SecretPointer!.ToString())!.AccessToken);
        _ = Assert.Single(_world.Ledger.Events, static e => e.Envelope.Type == EventTypes.ConnectionImported);
    }

    [Fact]
    public async Task A_second_start_imports_nothing()
    {
        await _world.Hold();
        _ = await Import().Run(Token);

        var again = await Import().Run(Token);

        Assert.False(again.Value);
        _ = Assert.Single(_world.Store.Rows);
        _ = Assert.Single(_world.Ledger.Events, static e => e.Envelope.Type == EventTypes.ConnectionImported);
    }

    [Fact]
    public async Task A_home_that_already_has_a_linear_connection_is_left_alone()
    {
        await _world.Hold();
        _ = await _world.Store.Upsert(ConnectionSamples.Oauth(), Token);

        var imported = await Import().Run(Token);

        Assert.False(imported.Value);
        Expect.Equal(0, _world.Ledger.Events.Count(static e => e.Envelope.Type == EventTypes.ConnectionImported));
    }
}
