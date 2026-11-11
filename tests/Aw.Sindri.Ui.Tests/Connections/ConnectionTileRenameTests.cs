using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Connections;

namespace Aw.Sindri.Ui.Tests.Connections;

/// <summary>Rename on a connected tile (#2556): the menu offers it, an inline field holds the current label, Save sends once, a refusal is said in plain words under the field and keeps it open.</summary>
[Lane(TestLane.Unit)]
public sealed class ConnectionTileRenameTests
{
    private readonly ConnectionsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ServiceTileViewModel> Connected(string id = "linear")
    {
        _ = _f.Server.SeedConnection(FakeSindriApi.ConnectionRow(id + ":work"));
        return (await _f.Shown()).Tiles.Single(t => string.Equals(t.Id, id, StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_menu_offers_Rename_on_a_connected_tile_and_not_on_an_offered_one()
    {
        var tile = await Connected();

        Assert.Equal((true, "Rename"), (tile.CanRename, tile.Text.Rename));
        Assert.False((await _f.Shown()).Tiles.Single(t => string.Equals(t.Id, "slack", StringComparison.Ordinal)).CanRename);
    }

    [Fact]
    public async Task Rename_opens_the_field_holding_the_current_label()
    {
        var tile = await Connected();

        tile.StartRename();

        Assert.Equal((true, "work"), (tile.IsRenaming, tile.RenameText));
    }

    [Fact]
    public async Task Cancel_closes_the_field_and_leaves_the_label_as_it_was()
    {
        var tile = await Connected();
        tile.StartRename();
        tile.RenameText = "home";

        tile.CancelRename();

        Assert.Equal((false, "work", "linear:work"), (tile.IsRenaming, tile.Account, tile.ConnectionName));
        Assert.DoesNotContain(_f.Server.ConnectionCallLog, c => c.StartsWith("rename", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Save_sends_the_typed_label_once_and_the_tile_shows_the_new_name()
    {
        var tile = await Connected();
        tile.StartRename();
        tile.RenameText = " home ";

        await tile.Rename(Token);

        Assert.Equal(["rename linear:work home"], [.. _f.Server.ConnectionCallLog.Where(c => c.StartsWith("rename", StringComparison.Ordinal))]);
        Assert.Equal((false, "linear:home", "home", string.Empty), (tile.IsRenaming, tile.ConnectionName, tile.Account, tile.RenameError));
    }

    [Fact]
    public async Task A_name_that_is_taken_keeps_the_field_open_and_says_so_without_a_code()
    {
        // The fake lists connections by name and a tile binds to the first of its service: "home" would sort before "work" and become the tile. "work2" sorts after it.
        _ = _f.Server.SeedConnection(FakeSindriApi.ConnectionRow("linear:work2"));
        var tile = await Connected();
        tile.StartRename();
        tile.RenameText = "work2";

        await tile.Rename(Token);

        Assert.Equal((true, "Another Linear connection is already called 'work2'. Pick another name."), (tile.IsRenaming, tile.RenameError));
        Assert.DoesNotContain("SND-", tile.RenameError, StringComparison.Ordinal);
        Assert.Equal("work", tile.Account);
    }

    [Fact]
    public async Task A_name_the_server_calls_bad_keeps_the_field_open_and_says_what_is_allowed()
    {
        var tile = await Connected();
        tile.StartRename();
        tile.RenameText = "Home Office";
        _f.Server.ConnectionWriteError = new Error("SND-CONN-008", ErrorKind.Invalid, "A connection name is <service>:<label>, lowercase letters, digits and '-'");

        await tile.Rename(Token);

        Assert.Equal((true, "Use lowercase letters, digits and '-' for the name."), (tile.IsRenaming, tile.RenameError));
        Assert.DoesNotContain("SND-", tile.RenameError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Saving_the_label_it_already_has_sends_nothing_and_closes_the_field()
    {
        var tile = await Connected();
        tile.StartRename();

        await tile.Rename(Token);

        Assert.False(tile.IsRenaming);
        Assert.DoesNotContain(_f.Server.ConnectionCallLog, c => c.StartsWith("rename", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_empty_label_cannot_be_saved(string typed)
    {
        var tile = await Connected();
        tile.StartRename();
        tile.RenameText = typed;

        await tile.Rename(Token);

        Assert.Equal((false, true), (tile.CanSaveRename, tile.IsRenaming));
        Assert.DoesNotContain(_f.Server.ConnectionCallLog, c => c.StartsWith("rename", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_label_line_is_hidden_while_renaming_and_back_after_Cancel()
    {
        var tile = await Connected();
        Assert.True(tile.ShowSubtitle);

        tile.StartRename();
        Assert.False(tile.ShowSubtitle);

        tile.CancelRename();
        Assert.True(tile.ShowSubtitle);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_empty_label_shows_the_reason_in_plain_words(string typed)
    {
        var tile = await Connected();
        tile.StartRename();

        tile.RenameText = typed;

        Assert.Equal((true, "Type a name."), (tile.HasRenameReason, tile.RenameReason));
    }

    [Fact]
    public async Task A_typed_label_has_no_reason_line()
    {
        var tile = await Connected();
        tile.StartRename();

        Assert.Equal((false, string.Empty), (tile.HasRenameReason, tile.RenameReason));
    }

    [Fact]
    public async Task After_a_taken_name_is_refused_the_text_stays_the_reason_shows_and_Save_stays_off_until_the_text_changes()
    {
        _ = _f.Server.SeedConnection(FakeSindriApi.ConnectionRow("linear:work2"));
        var tile = await Connected();
        tile.StartRename();
        tile.RenameText = "work2";

        await tile.Rename(Token);

        Assert.Equal(("work2", true, false), (tile.RenameText, tile.HasRenameReason, tile.CanSaveRename));
        Assert.Contains("work2", tile.RenameReason, StringComparison.Ordinal);

        tile.RenameText = "work3";

        Assert.True(tile.CanSaveRename);
    }
}
