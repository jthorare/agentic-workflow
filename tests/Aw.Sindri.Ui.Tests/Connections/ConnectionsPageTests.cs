using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Connections;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;

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
        Assert.Equal("3 connections · 2 need you", page.Head);
        Assert.Equal([false, true, true], page.Rows.Select(r => r.CanSignIn));   // a key connection has no browser sign-in (SND-CONN-011)
        Assert.Equal("Refreshed 30m ago", page.Rows[1].RefreshedText);
    }

    [Fact]
    public async Task An_empty_list_says_so()
    {
        var page = await _f.Shown();

        Assert.Equal((true, false, 0), (page.IsEmpty, page.IsReading, page.Rows.Count));
        Assert.Equal(Labels.ConnectionsEmpty, page.EmptyText);
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

        await _f.Drive(row.SignIn(Token));

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
        _f.Server.ConnectsAfterPolls = int.MaxValue;
        var page = await _f.Shown();

        await _f.Drive(page.Rows[2].SignIn(Token));

        Assert.Equal(Labels.ConnectionSignInTimedOut, page.Rows[2].NoteText);
        Assert.Equal("! Needs sign-in", page.Rows[2].ChipText);
    }
}
