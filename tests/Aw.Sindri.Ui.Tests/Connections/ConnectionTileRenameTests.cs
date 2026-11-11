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

    private static int Renames(FakeSindriApi server) => server.ConnectionCallLog.Count(c => c.StartsWith("rename", StringComparison.Ordinal));

    [Fact]
    public async Task When_the_connection_goes_away_the_open_field_closes()
    {
        var tile = await Connected();
        tile.StartRename();
        tile.RenameText = "half-typed";

        tile.Bind(row: null);

        Assert.Equal((false, false), (tile.IsRenaming, tile.CanSaveRename));
    }

    [Fact]
    public async Task When_the_tile_rebinds_to_another_connection_the_open_field_closes_and_nothing_is_renamed()
    {
        var tile = await Connected();
        tile.StartRename();
        tile.RenameText = "half-typed";

        tile.Bind(new ConnectionRowViewModel(FakeSindriApi.ConnectionRow("linear:home"), _f.Actions(), _f.Clock));
        await tile.Rename(Token);

        Assert.Equal((false, 0), (tile.IsRenaming, Renames(_f.Server)));
    }

    [Fact]
    public async Task A_read_that_binds_the_same_connection_again_keeps_the_field_and_the_typed_text()
    {
        var tile = await Connected();
        tile.StartRename();
        tile.RenameText = "half-typed";

        tile.Bind(new ConnectionRowViewModel(FakeSindriApi.ConnectionRow("linear:work"), _f.Actions(), _f.Clock));

        Assert.Equal((true, "half-typed"), (tile.IsRenaming, tile.RenameText));
    }

    [Fact]
    public async Task Save_pressed_twice_before_the_answer_sends_one_rename()
    {
        var tile = await Connected();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _f.Server.RenameGate = gate;
        tile.StartRename();
        tile.RenameText = "home";

        var first = tile.Rename(Token);
        await tile.Rename(Token);
        gate.SetResult();
        await first;

        Assert.Equal(1, Renames(_f.Server));
    }

    [Fact]
    public async Task Saving_the_label_with_spaces_around_it_sends_nothing()
    {
        var tile = await Connected();
        tile.StartRename();
        tile.RenameText = "  work  ";

        await tile.Rename(Token);

        Assert.Equal((false, 0), (tile.IsRenaming, Renames(_f.Server)));
    }

    [Fact]
    public async Task An_error_code_the_app_does_not_know_shows_one_fixed_sentence_with_none_of_the_server_text()
    {
        var tile = await Connected();
        tile.StartRename();
        tile.RenameText = "home";
        _f.Server.ConnectionWriteError = new Error("SND-X-999", ErrorKind.Invalid, "boom at 10.0.0.7:9000 stack trace");

        await tile.Rename(Token);

        Assert.Equal((true, "Couldn't rename it. Try again."), (tile.IsRenaming, tile.RenameError));
    }

    [Fact]
    public async Task A_call_that_throws_shows_the_fixed_sentence_keeps_the_field_and_lets_Save_try_again()
    {
        var tile = await Connected();
        tile.StartRename();
        tile.RenameText = "home";
        _f.Server.RenameThrows = new HttpRequestException("connection reset 10.0.0.7");

        await tile.Rename(Token);

        Assert.Equal((true, "Couldn't rename it. Try again.", true), (tile.IsRenaming, tile.RenameError, tile.CanSaveRename));
    }

    [Fact]
    public async Task The_tile_shows_the_new_name_from_the_answer_even_when_the_re_read_fails()
    {
        var tile = await Connected();
        tile.StartRename();
        tile.RenameText = "home";
        _f.Server.NextConnectionListError = new Error("SND-X-001", ErrorKind.Invalid, "down");

        await tile.Rename(Token);

        Assert.Equal((false, "linear:home", "home"), (tile.IsRenaming, tile.ConnectionName, tile.Account));
    }

    [Fact]
    public async Task A_case_only_change_the_server_calls_the_same_name_reads_as_a_plain_sentence()
    {
        var tile = await Connected();
        tile.StartRename();
        tile.RenameText = "Work";
        _f.Server.ConnectionWriteError = new Error("SND-CLI-003", ErrorKind.Invalid, "the new name is the current one");

        await tile.Rename(Token);

        Assert.Equal((true, "That's already its name."), (tile.IsRenaming, tile.RenameError));
    }

    [Fact]
    public async Task A_late_success_after_the_tile_rebinds_does_not_rename_the_other_connections_row()
    {
        var tile = await Connected();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _f.Server.RenameGate = gate;
        tile.StartRename();
        tile.RenameText = "home";
        var sending = tile.Rename(Token);
        tile.Bind(new ConnectionRowViewModel(FakeSindriApi.ConnectionRow("linear:other"), _f.Actions(), _f.Clock));

        gate.SetResult();
        await sending;

        Assert.Equal(("linear:other", false, string.Empty), (tile.ConnectionName, tile.IsRenaming, tile.RenameError));
    }

    [Fact]
    public async Task A_late_refusal_after_the_tile_rebinds_leaves_the_new_binding_alone()
    {
        var tile = await Connected();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _f.Server.RenameGate = gate;
        tile.StartRename();
        tile.RenameText = "home";
        var sending = tile.Rename(Token);
        tile.Bind(new ConnectionRowViewModel(FakeSindriApi.ConnectionRow("linear:other"), _f.Actions(), _f.Clock));
        _f.Server.ConnectionWriteError = new Error("SND-CONN-010", ErrorKind.Conflict, "taken");

        gate.SetResult();
        await sending;
        tile.StartRename();
        tile.RenameText = "home";

        Assert.Equal((string.Empty, true), (tile.RenameError, tile.CanSaveRename));
    }

    [Fact]
    public async Task An_unknown_error_code_still_lets_Save_send_the_same_name_again()
    {
        var tile = await Connected();
        tile.StartRename();
        tile.RenameText = "home";
        _f.Server.ConnectionWriteError = new Error("SND-X-999", ErrorKind.Invalid, "boom");
        await tile.Rename(Token);

        Assert.True(tile.CanSaveRename);
        _f.Server.ConnectionWriteError = null;
        await tile.Rename(Token);

        Assert.Equal((2, "home"), (Renames(_f.Server), tile.Account));
    }
}
