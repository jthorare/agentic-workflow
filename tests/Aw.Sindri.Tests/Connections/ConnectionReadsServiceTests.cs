using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Features.GetConnection;
using Aw.Sindri.Features.ListConnections;
using Aw.Sindri.Testing.Connections;

namespace Aw.Sindri.Tests.Connections;

[Lane(TestLane.Unit)]
public sealed class ConnectionReadsServiceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task List_shows_every_connection_sorted_with_whether_a_secret_is_stored_and_nothing_more()
    {
        var world = new ConnectionHarness();
        _ = await world.Store.Upsert(ConnectionSamples.Oauth(ConnectionSamples.Name("slack", "team")), Token);
        _ = await world.Store.Upsert(ConnectionSamples.Oauth(ConnectionSamples.Name("github", "work"), ConnectionStatus.NeedsSignIn), Token);

        var listed = await world.List().Handle(new ListConnectionsRequest(), Token);

        Assert.Equal(["github:work", "slack:team"], listed.Value.Connections.Select(static c => c.Name), StringComparer.Ordinal);
        var github = listed.Value.Connections[0];
        Assert.Equal("oauth", github.Kind);
        Assert.Equal("needs-sign-in", github.Status);
        Assert.Equal("the provider refused the refresh", github.StatusReason);
        Assert.True(github.HasSecret);
    }

    [Fact]
    public async Task Get_shows_one_connection_without_the_pointer_or_any_token()
    {
        var world = new ConnectionHarness();
        _ = await world.Store.Upsert(ConnectionSamples.Oauth(), Token);

        var got = await world.Get().Handle(new GetConnectionRequest("linear:work"), Token);

        Assert.True(got.IsSuccess);
        Assert.Equal("linear:work", got.Value.Connection.Name);
        Assert.Equal("client-123", got.Value.Connection.Config["clientId"]);
        Assert.DoesNotContain("sindri-connection", System.Text.Json.JsonSerializer.Serialize(got.Value.Connection), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_of_an_unknown_name_is_SND_CONN_001_and_of_a_bad_name_SND_CONN_008()
    {
        var world = new ConnectionHarness();

        var unknown = await world.Get().Handle(new GetConnectionRequest("linear:nobody"), Token);
        var bad = await world.Get().Handle(new GetConnectionRequest("LINEAR"), Token);

        Assert.Equal(SindriErrors.ConnectionNotFound.Code, unknown.Error.Code);
        Assert.Equal(SindriErrors.ConnectionNameInvalid.Code, bad.Error.Code);
    }
}
