using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Connections;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Connections;

namespace Aw.Sindri.Ui.Tests.Connections;

/// <summary>The Connections page: the list with its status chips, and each row's actions (G8: the same actions as the MCP tools, no more).</summary>
[Lane(TestLane.Unit)]
public sealed class ConnectionsPageTests
{
    private readonly ConnectionsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_list_shows_status_chips_and_a_needs_sign_in_row_offers_Sign_in_again()
    {
        _f.SeedThree();

        var page = await _f.Shown();

        Assert.Equal(["acme:key", "linear:work", "notes:mcp"], page.Rows.Select(r => r.Name), StringComparer.Ordinal);
        Assert.Equal(["✕ Error", "✓ Connected", "! Needs sign-in"], page.Rows.Select(r => r.ChipText), StringComparer.Ordinal);
        Assert.Equal("the provider refused the refresh", page.Rows[0].ReasonText);
        Assert.Equal("1 of 5 connected", page.Head);   // linear:work is on the Linear tile
        Assert.Equal([false, true, true], page.Rows.Select(r => r.CanSignIn));   // a key connection has no browser sign-in (SND-CONN-011)
        Assert.Equal("Refreshed 30m ago", page.Rows[1].RefreshedText);
    }

    [Fact]
    public async Task An_empty_list_shows_the_tiles_and_no_box()
    {
        var page = await _f.Shown();

        Assert.Equal((false, false, 0), (page.IsEmpty, page.IsReading, page.Rows.Count));
        Assert.Equal(5, page.Tiles.Count);
    }

    [Fact]
    public async Task Remove_asks_first_and_drops_the_row_after_the_API_confirms()
    {
        _f.SeedThree();
        var page = await _f.Shown();
        var row = page.Rows[0];

        row.AskRemove();

        Assert.True(row.IsConfirmingRemove);
        Assert.DoesNotContain("remove acme:key", _f.Server.ConnectionCallLog, StringComparer.Ordinal);
        Assert.Equal(3, page.Rows.Count);

        await row.ConfirmRemove(Token);

        Assert.Contains("remove acme:key", _f.Server.ConnectionCallLog, StringComparer.Ordinal);
        Assert.Equal(["linear:work", "notes:mcp"], page.Rows.Select(r => r.Name), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Keeping_a_connection_sends_nothing()
    {
        _f.SeedThree();
        var page = await _f.Shown();

        page.Rows[0].AskRemove();
        page.Rows[0].Keep();

        Assert.False(page.Rows[0].IsConfirmingRemove);
        Assert.DoesNotContain(_f.Server.ConnectionCallLog, c => c.StartsWith("remove", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_refused_remove_keeps_the_row_and_says_why()
    {
        _f.SeedThree();
        var page = await _f.Shown();
        _f.Server.ConnectionWriteError = new Error("SND-CONN-006", ErrorKind.Unavailable, "The secret store is locked.");

        page.Rows[0].AskRemove();
        await page.Rows[0].ConfirmRemove(Token);

        Assert.Equal(3, page.Rows.Count);
        Assert.Contains("SND-CONN-006", page.Rows[0].NoteText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Test_shows_ok_or_the_reason()
    {
        _f.SeedThree();
        var failing = FakeSindriApi.ConnectionRow("acme:key", "error", "rest", "the provider refused the refresh");
        _f.Server.ConnectionTests["acme:key"] = new ConnectionTested(failing, Ok: false, "The connection needs a new sign-in (SND-CONN-002)", Tools: null);
        var page = await _f.Shown();

        await page.Rows[1].Test(Token);
        await page.Rows[0].Test(Token);

        Assert.Equal("Test passed", page.Rows[1].NoteText);
        Assert.Equal("Test failed: The connection needs a new sign-in (SND-CONN-002)", page.Rows[0].NoteText);
    }

    [Fact]
    public async Task Sign_in_again_opens_the_consent_URL_and_the_row_turns_connected()
    {
        _f.SeedThree();
        var page = await _f.Shown();
        var row = page.Rows[2];

        await _f.Drive(row.SignIn(Token), "notes:mcp");

        Assert.Equal([_f.Server.ConsentUrl], _f.Launcher.Opened, StringComparer.Ordinal);
        Assert.Equal(("✓ Connected", Labels.ConnectionSignedIn), (row.ChipText, row.NoteText));
        Assert.Contains("signin notes:mcp", _f.Server.ConnectionCallLog, StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_key_connection_is_never_asked_to_sign_in()
    {
        _f.SeedThree();
        var page = await _f.Shown();

        await page.Rows[0].SignIn(Token);

        Assert.DoesNotContain(_f.Server.ConnectionCallLog, c => c.StartsWith("signin", StringComparison.Ordinal));
        Assert.Empty(_f.Launcher.Opened);
    }

    [Fact]
    public async Task A_sign_in_that_never_lands_ends_with_a_timeout_note()
    {
        _f.SeedThree();
        var page = await _f.Shown();

        await _f.Drive(page.Rows[2].SignIn(Token));

        Assert.Equal(Labels.ConnectionSignInTimedOut, page.Rows[2].NoteText);
        Assert.Equal("! Needs sign-in", page.Rows[2].ChipText);
    }

    [Fact]
    public async Task Leaving_the_page_stops_a_sign_in_wait_and_frees_the_row()
    {
        _f.SeedThree();
        var page = await _f.Shown();
        var row = page.Rows[2];
        var run = row.SignIn(Token);
        await _f.Drive(Task.Delay(30, Token));   // some polls, no callback
        Assert.False(run.IsCompleted);

        page.SetShown(shown: false);
        await run.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, Token);   // real time: the fake clock never runs out on its own
        var reads = _f.Server.ConnectionCallLog.Count(c => string.Equals(c, "get notes:mcp", StringComparison.Ordinal));
        _f.Clock.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal((false, string.Empty), (row.IsBusy, row.NoteText));
        Assert.Equal(reads, _f.Server.ConnectionCallLog.Count(c => string.Equals(c, "get notes:mcp", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Signing_in_again_on_a_connected_row_waits_for_a_new_sign_in_not_the_status_it_already_has()
    {
        _f.SeedThree();
        var page = await _f.Shown();
        var row = page.Rows[1];   // linear:work reads 'connected' before the sign-in starts

        var run = row.SignIn(Token);
        await _f.Drive(Task.Delay(30, Token), "linear:work", afterReads: 100);   // a minute of polls, no callback
        Assert.False(run.IsCompleted);
        Assert.NotEqual(Labels.ConnectionSignedIn, row.NoteText, StringComparer.Ordinal);

        _f.Server.CompleteSignIn("linear:work");
        await _f.Drive(run);

        Assert.Equal(Labels.ConnectionSignedIn, row.NoteText);
    }

    [Fact]
    public async Task A_failed_sign_in_again_on_a_connected_row_ends_on_the_failure_without_waiting_out_the_5_minutes()
    {
        _f.SeedThree();
        var page = await _f.Shown();
        var row = page.Rows[1];   // linear:work reads 'connected' before the sign-in starts
        var run = row.SignIn(Token);
        await _f.Drive(Task.Delay(30, Token));
        Assert.False(run.IsCompleted);

        _f.Server.FailSignIn("linear:work", "SND-CONN-009");
        var before = _f.Clock.GetUtcNow();
        await _f.Drive(run);

        Assert.True(_f.Clock.GetUtcNow() - before <= ConnectionActions.PollEvery * 3, "the poll should stop on the failure, not wait out the 5 minutes");
        Assert.Contains("SND-CONN-009", row.NoteText, StringComparison.Ordinal);
        Assert.Equal("✓ Connected", row.ChipText);   // the row still works on its old tokens
    }

    [Fact]
    public async Task Signing_in_again_on_an_error_row_keeps_waiting_while_the_browser_is_open()
    {
        _f.SeedThree();
        _ = _f.Server.SeedConnection(FakeSindriApi.ConnectionRow("linear:work", "error", "oauth", "the provider refused the refresh"));
        var page = await _f.Shown();
        var row = page.Rows[1];

        var run = row.SignIn(Token);
        await _f.Drive(Task.Delay(30, Token), "linear:work", afterReads: 100);
        Assert.False(run.IsCompleted);

        await _f.Drive(run, "linear:work", afterReads: 0);

        Assert.Equal((Labels.ConnectionSignedIn, "✓ Connected"), (row.NoteText, row.ChipText));
    }

    [Fact]
    public async Task A_removal_drops_a_row_that_a_list_read_already_in_flight_still_holds()
    {
        _f.SeedThree();
        var page = await _f.Shown();
        var gate = new TaskCompletionSource();
        _f.Server.ConnectionListGate = gate;
        var read = page.Refresh(ConnectionsFixture.Url, Token);   // answers with all three rows once the gate opens
        _f.Server.ConnectionListGate = null;

        page.Rows[0].AskRemove();
        await page.Rows[0].ConfirmRemove(Token);
        gate.SetResult();
        await read;

        Assert.Equal(["linear:work", "notes:mcp"], page.Rows.Select(r => r.Name), StringComparer.Ordinal);
    }
}
