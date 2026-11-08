using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections.OAuth;
using Aw.Sindri.Features.RemoveConnection;
using Aw.Sindri.Testing.Connections;

namespace Aw.Sindri.Tests.Connections;

[Lane(TestLane.Unit)]
public sealed class RemoveConnectionServiceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Remove_deletes_the_secret_and_the_row()
    {
        var world = new ConnectionHarness();
        var stored = world.Secrets.Store(ConnectionSamples.Name(), new TokenSet("access-1", "refresh-1", ExpiresAt: null)).Value;
        _ = await world.Store.Upsert(ConnectionSamples.Oauth(), Token);

        var removed = await world.Remove().Handle(new RemoveConnectionRequest("linear:work"), Token);

        Assert.True(removed.IsSuccess);
        Assert.True(removed.Value.SecretDeleted);
        Assert.Equal([stored.ToString()], world.Secrets.Deleted);
        Assert.Empty(world.Secrets.Pointers);
        Assert.Empty(world.Store.Rows);
    }

    [Fact]
    public async Task An_unknown_name_is_SND_CONN_001()
    {
        var world = new ConnectionHarness();

        var removed = await world.Remove().Handle(new RemoveConnectionRequest("linear:nobody"), Token);

        Assert.Equal(SindriErrors.ConnectionNotFound.Code, removed.Error.Code);
    }

    [Fact]
    public async Task A_secret_that_cannot_be_deleted_keeps_the_row_so_the_pointer_is_not_lost()
    {
        var world = new ConnectionHarness();
        _ = await world.Store.Upsert(ConnectionSamples.Oauth(), Token);
        world.Secrets.Fails = true;

        var removed = await world.Remove().Handle(new RemoveConnectionRequest("linear:work"), Token);

        Assert.Equal(SindriErrors.ConnectionSecretUnavailable.Code, removed.Error.Code);
        _ = Assert.Single(world.Store.Rows);
    }
}
