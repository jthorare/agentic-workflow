using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Connections;

namespace Aw.Sindri.Ui.Tests.Connections;

/// <summary>#4396: an OAuth tile with an empty client ID asked for it twice, in the help line ("...then paste its client ID here.") and again under the field ("Enter your Linear app's client ID"). The help line says it, so the inline reason stays quiet; Connect stays disabled and its tooltip still says why.</summary>
[Lane(TestLane.Unit)]
public sealed class ConnectionClientIdOnceTests
{
    private readonly ConnectionsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ServiceTileViewModel> Tile(string id) => (await _f.Shown()).Tiles.Single(t => string.Equals(t.Id, id, StringComparison.Ordinal));

    [Theory]
    [InlineData("linear", "Linear")]
    [InlineData("spotify", "Spotify")]
    public async Task An_OAuth_tile_with_no_client_ID_shows_its_help_line_and_no_second_inline_reason_and_Connect_stays_disabled(string id, string title)
    {
        var tile = await Tile(id);
        await tile.Connect(Token);
        if (tile.ShowUseOAuth)
        {
            tile.UseOAuth();
        }

        Assert.NotEmpty(tile.HelpText);
        Assert.Contains("client ID", tile.HelpText, StringComparison.Ordinal);
        Assert.False(tile.HasBlockedReason);
        Assert.False(tile.CanPress);
        Assert.False(tile.CanSend);
        Assert.Equal($"Enter your {title} app's client ID", tile.BlockedReason);   // the tooltip of the disabled button still says why
    }

    [Fact]
    public async Task A_service_with_no_help_line_still_says_the_client_ID_is_missing()
    {
        var tile = new ServiceTileViewModel(new ServiceTile("acme", "Acme", "Anything", ServiceTileMode.ClientId), _f.Actions(), () => Task.CompletedTask, _f.Clock);
        await tile.Connect(Token);

        Assert.Empty(tile.HelpText);
        Assert.True(tile.HasBlockedReason);
        Assert.Equal("Enter your Acme app's client ID", tile.BlockedReason);
    }

    [Fact]
    public async Task A_missing_client_secret_is_still_said_inline_once_the_client_ID_is_typed()
    {
        var tile = await Tile("github");
        await tile.Connect(Token);
        tile.UseOAuth();
        tile.ClientId = "client-123";

        Assert.True(tile.HasBlockedReason);
        Assert.Equal("Paste your GitHub app's client secret", tile.BlockedReason);
    }
}
