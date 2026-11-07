using Aw.Kernel.Testing;

namespace Aw.Sindri.Ui.Tests.Linear;

/// <summary>When the issue sheet may be edited (Linear hub spec §3, "Write-access banner"): only on a write that says allowed; every other case reads as text.</summary>
[Lane(TestLane.Unit)]
public sealed class LinearIssueEditGateTests
{
    private readonly LinearIssueEditFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_writable_issue_is_editable_with_no_banner()
    {
        var sheet = await _f.Opened(Token);

        Assert.Equal((true, string.Empty, false), (sheet.CanEdit, sheet.WriteBanner, sheet.CanConnect));
    }

    [Fact]
    public async Task An_answer_without_a_write_member_is_not_editable()
    {
        var issue = LinearIssueEditFixture.Issue("writable");
        _ = issue.Remove("write");
        _f.Serve(issue);

        _ = await _f.Opened(Token);

        Assert.Equal((false, string.Empty, false), (_f.Sheet.CanEdit, _f.Sheet.WriteBanner, _f.Sheet.CanConnect));
        Assert.Empty(_f.Server.LinearLookupsRead);
    }

    [Fact]
    public async Task No_write_key_says_edits_need_write_access_and_offers_connect_which_asks_the_shell_for_settings()
    {
        _f.Serve(LinearIssueEditFixture.Issue("no-write-key"));
        var asked = 0;
        _f.Sheet.ConnectRequested += (_, _) => asked++;

        _ = await _f.Opened(Token);
        _f.Sheet.Connect();

        Assert.Equal((false, "Edits need write access.", true, "Connect", 1), (_f.Sheet.CanEdit, _f.Sheet.WriteBanner, _f.Sheet.CanConnect, _f.Sheet.ConnectText, asked));
    }

    [Fact]
    public async Task A_team_that_was_not_picked_says_so_without_a_connect_button()
    {
        _f.Serve(LinearIssueEditFixture.Issue("team-not-picked"));

        _ = await _f.Opened(Token);

        Assert.Equal((false, "ENG isn't one of your teams in Sindri.", false), (_f.Sheet.CanEdit, _f.Sheet.WriteBanner, _f.Sheet.CanConnect));
    }

    [Fact]
    public async Task A_session_read_shows_the_issue_read_only_with_no_banner()
    {
        _f.Serve(LinearIssueEditFixture.Issue("session-write-refused"));

        _ = await _f.Opened(Token);

        Assert.Equal((false, string.Empty, false), (_f.Sheet.CanEdit, _f.Sheet.WriteBanner, _f.Sheet.CanConnect));
    }

    [Fact]
    public async Task A_removed_issue_keeps_its_note_and_turns_edits_off()
    {
        _f.Serve(LinearIssueEditFixture.Issue("removed"));

        _ = await _f.Opened(Token);

        Assert.Equal((false, false, true), (_f.Sheet.CanEdit, _f.Sheet.CanSave, _f.Sheet.RemovedNote.Length > 0));
    }

    [Fact]
    public async Task A_save_the_server_refuses_for_a_missing_write_key_shows_the_banner_and_connect()
    {
        var sheet = await _f.Opened(Token);
        _f.Server.QueueLinearChange(LinearIssueEditFixture.Change("no-write-key"));
        _f.Pick("Priority", "High");

        await sheet.Save(Token);

        Assert.Equal(("Edits need write access.", true, true), (sheet.ErrorBanner, sheet.CanConnect, sheet.HasChanges));
    }

    [Fact]
    public async Task A_save_the_server_refuses_for_a_team_not_picked_says_so()
    {
        var sheet = await _f.Opened(Token);
        _f.Server.QueueLinearChange(LinearIssueEditFixture.Change("team-not-picked"));
        _f.Pick("Priority", "High");

        await sheet.Save(Token);

        Assert.Equal(("ENG isn't one of your teams in Sindri.", false), (sheet.ErrorBanner, sheet.CanConnect));
    }
}
