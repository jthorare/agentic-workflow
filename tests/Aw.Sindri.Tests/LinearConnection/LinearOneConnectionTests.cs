using System.Text.Json;
using Aw.Context.Testing.Engine;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Features.LinearConnection;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Connections;
using Aw.Sindri.Tests.LinearReads;

namespace Aw.Sindri.Tests.LinearConnection;

/// <summary>There is one Linear connection (#3364): the one on the Connections page. Settings and the Linear context source read it, whichever place it was connected in.</summary>
[Lane(TestLane.Unit)]
public sealed class LinearOneConnectionTests
{
    private static readonly DateTimeOffset _at = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);

    private readonly InMemoryConnectionStore _connections = new();
    private readonly InMemoryProfiles _profiles = new();

    public LinearOneConnectionTests()
    {
        var profile = new SindriProfile(1, "thor", Context: JsonDocument.Parse("{\"sources\":{\"linear\":{\"teams\":[\"ENG\"]}}}").RootElement.Clone());
        _profiles.Live = new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml");
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ContextLinearConnectionFacts Facts() => new(_profiles, FakeContextEngine.Instance, new FakeLinearSideFacts(), new FakeLinearCheckStore(), _connections);

    private async Task Connect(ConnectionStatus status)
    {
        var name = ConnectionName.Parse("linear:work").Value;
        var pointer = SecretPointer.Parse("keychain:sindri-connection/linear.work").Value;
        var row = new Connection(name, ConnectionKind.Rest, "linear", new Dictionary<string, string>(), pointer, status, StatusReason: null, "human", _at, _at, LastRefreshedAt: null);
        _ = await _connections.Insert(row, Token);
    }

    [Fact]
    public async Task A_linear_connection_made_on_the_Connections_page_reads_as_connected_in_Settings_with_no_profile_pointer()
    {
        await Connect(ConnectionStatus.Connected);

        var view = (await Facts().Read(Token)).Value;

        Expect.Equal("resolves", view.Read.State);
    }

    [Fact]
    public async Task A_linear_connection_that_needs_sign_in_does_not_read_as_connected()
    {
        await Connect(ConnectionStatus.NeedsSignIn);

        var view = (await Facts().Read(Token)).Value;

        Expect.Equal("notSet", view.Read.State);
    }
}
