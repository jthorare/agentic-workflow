using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using static Aw.Sindri.Ui.Tests.Jobs.JobsFixture;

namespace Aw.Sindri.Ui.Tests.Jobs;

/// <summary>The Jobs page's list (API P6): the rows in words, the served empty sentence, the kinds and states this build doesn't know, Cancel, and when the page reads.</summary>
[Lane(TestLane.Unit)]
public sealed class JobsPageTests
{
    private readonly JobsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_running_job_reads_in_words_with_its_head_and_Cancel()
    {
        var page = await _f.Shown(Token);

        var row = Assert.Single(page.Rows);
        Assert.Equal(("Echo job", "● Running", "Started 5m ago", "by Sindri", string.Empty), (row.TitleText, row.ChipText, row.WhenText, row.ByText, row.SummaryText));
        Assert.Equal("1 running · 1 job", page.Head);
        Assert.Equal("Jobs, 1 running · 1 job", page.HeadName);
        Assert.Equal(("Cancel job", true), (row.Cancel?.Label, row.HasCancel));
        Assert.Equal("Echo job, running, Started 5m ago, by Sindri", row.Name);
        Assert.False(page.IsReading);
    }

    [Fact]
    public async Task A_finished_job_names_its_service_when_it_ended_and_how()
    {
        _f.ServeList("by-kind");

        var row = Assert.Single((await _f.Shown(Token)).Rows);

        Assert.Equal(("Run a service", "heartbeat", "✓ Done", "Ended 5m ago", "ok: worked"), (row.TitleText, row.KeyText, row.ChipText, row.WhenText, row.SummaryText));
        Assert.Null(row.Cancel);
        Assert.False(row.IsRunning);
    }

    [Fact]
    public async Task A_failed_job_shows_its_code_after_the_summary()
    {
        var list = Golden("jobs", "by-kind");
        var served = list["jobs"]![0]!;
        served["state"] = "failed";
        served["summary"] = "it broke";
        served["code"] = "SND-SCHED-001";
        _f.ServeList(list);

        var row = Assert.Single((await _f.Shown(Token)).Rows);

        Assert.Equal(("✕ Failed", "it broke", "SND-SCHED-001", true), (row.ChipText, row.SummaryText, row.CodeText, row.HasCode));
    }

    [Fact]
    public async Task A_cancelled_job_is_cancelled_in_its_own_word()
    {
        var list = Golden("jobs", "running");
        list["jobs"]![0]!["state"] = "cancelled";
        _f.ServeList(list);

        var row = Assert.Single((await _f.Shown(Token)).Rows);

        Assert.Equal(("○ Cancelled", false), (row.ChipText, row.HasCancel));
    }

    [Fact]
    public async Task A_kind_and_a_state_this_build_does_not_know_render_by_their_served_words()
    {
        var list = Golden("jobs", "running");
        list["jobs"]![0]!["kind"] = "TidyUpScratchFiles";
        list["jobs"]![0]!["state"] = "paused";
        _f.ServeList(list);

        var row = Assert.Single((await _f.Shown(Token)).Rows);

        Assert.Equal(("Tidy up scratch files", "○ Paused"), (row.TitleText, row.ChipText));
    }

    [Theory]
    [InlineData("service:host", "by Sindri")]
    [InlineData("human", "by you")]
    [InlineData("service:sync", "by Sindri's sync service")]
    [InlineData("session:abc", "by an agent session")]
    [InlineData("something-new", "by something-new")]
    public async Task Who_started_it_is_said_in_words(string startedBy, string expected)
    {
        var list = Golden("jobs", "running");
        list["jobs"]![0]!["startedBy"] = startedBy;
        _f.ServeList(list);

        Assert.Equal(expected, Assert.Single((await _f.Shown(Token)).Rows).ByText);
    }

    [Fact]
    public async Task The_empty_list_says_what_the_API_says()
    {
        _f.ServeList("empty");

        var page = await _f.Shown(Token);

        Assert.Equal((true, (string)Golden("jobs", "empty")["empty"]!, 0), (page.IsEmpty, page.EmptyText, page.Rows.Count));
    }

    [Fact]
    public async Task A_read_keeps_the_row_it_already_has()
    {
        var page = await _f.Shown(Token);
        var first = page.Rows[0];
        var list = Golden("jobs", "running");
        list["jobs"]![0]!["rounds"] = 3;
        _f.ServeList(list);

        await page.Refresh(Url, Token);

        Assert.Same(first, Assert.Single(page.Rows));
        Assert.Equal(2, page.Drawn);
    }

    [Fact]
    public async Task A_job_that_finishes_loses_its_Cancel_in_place()
    {
        var page = await _f.Shown(Token);
        var row = page.Rows[0];
        var list = Golden("jobs", "running");
        list["jobs"]![0]!["state"] = "succeeded";
        list["jobs"]![0]!["endedAt"] = "2026-01-01T00:04:00.0000000Z";
        _f.ServeList(list);

        await page.Refresh(Url, Token);

        Assert.Equal((true, "✓ Done", "Ended 1m ago", false), (ReferenceEquals(row, page.Rows[0]), row.ChipText, row.WhenText, row.HasCancel));
    }

    [Fact]
    public async Task A_job_that_is_gone_from_the_list_leaves_the_page()
    {
        var page = await _f.Shown(Token);
        _f.ServeList("by-kind");

        await page.Refresh(Url, Token);

        Assert.Equal("JOB-00000000000000000000000000000002", Assert.Single(page.Rows).Id);
    }

    [Fact]
    public async Task The_page_only_remembers_where_the_API_is_until_it_is_shown_then_reads_at_once()
    {
        var page = _f.Page();

        await page.Refresh(Url, Token);
        Assert.Equal((0, true), (_f.Server.JobsCalls, page.IsReading));
        page.SetShown(shown: true);

        for (var i = 0; i < 400 && page.Drawn == 0; i++)
        {
            await Task.Delay(5, Token);
        }

        Assert.Equal((1, 1, false), (page.Drawn, _f.Server.JobsCalls, page.IsReading));
    }

    [Fact]
    public async Task A_page_that_is_left_reads_nothing_more()
    {
        var page = await _f.Shown(Token);
        page.SetShown(shown: false);

        await page.Refresh(Url, Token);

        Assert.Equal(1, _f.Server.JobsCalls);
    }

    [Fact]
    public async Task A_failed_read_keeps_the_last_rows_and_marks_them_stale()
    {
        var page = await _f.Shown(Token);
        _f.Server.JobsAnswer = RouteAnswer.Problem(System.Net.HttpStatusCode.ServiceUnavailable, "SND-API-011", "The views have not caught up");

        await page.Refresh(Url, Token);

        Assert.Equal((1, true, false), (page.Rows.Count, page.IsStale, page.IsUnreadable));
    }

    [Fact]
    public async Task An_answer_this_build_cannot_read_says_so()
    {
        _f.Server.JobsAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, "[]");
        var page = _f.Page();
        page.SetShown(shown: true);

        await page.Refresh(Url, Token);

        Assert.Equal((false, true, Labels.JobsUnreadable), (page.IsReading, page.IsUnreadable, page.UnreadableText));
    }

    [Fact]
    public async Task Cancel_sends_the_command_once_for_that_job()
    {
        var page = await _f.Shown(Token);

        await page.Rows[0].Cancel!.Press(Token);

        Assert.Equal([Id], _f.Server.CancelledJobs, StringComparer.Ordinal);
        Assert.True(page.Rows[0].Cancel!.IsAccepted);
    }

    [Fact]
    public async Task Cancel_by_a_session_shows_the_refusal_as_served()
    {
        _f.Server.CancelJobAnswer = RouteAnswer.Problem("jobs-id-cancel", "refused-session");
        var page = await _f.Shown(Token);

        await page.Rows[0].Cancel!.Press(Token);

        var cancel = page.Rows[0].Cancel!;
        Assert.Equal(("SND-AUTH-002", (string)Golden("jobs-id-cancel", "refused-session")["detail"]!), (cancel.ErrorCode, cancel.ErrorDetail));
    }

    [Fact]
    public async Task A_running_job_gets_its_Cancel_when_the_catalog_arrives_after_the_rows()
    {
        var late = new JobsFixture(catalog: false);
        var page = await late.Shown(Token);
        Assert.False(page.Rows[0].HasCancel);

        late.ShowCatalog();

        Assert.True(page.Rows[0].HasCancel);
    }

    [Fact]
    public async Task A_row_asking_to_open_hands_the_shell_its_job_link()
    {
        var page = await _f.Shown(Token);
        DeepLink? asked = null;
        page.OpenRequested += (_, e) => asked = e.Link;

        page.RequestOpen(page.Rows[0].Link);

        Assert.Equal("job/" + Id, asked?.ToString());
    }

    [Fact]
    public async Task The_page_never_shows_a_raw_id_or_a_served_code_as_its_title()
    {
        var page = await _f.Shown(Token);

        Assert.DoesNotContain("JOB-", page.Rows[0].TitleText, StringComparison.Ordinal);
        Assert.DoesNotContain("JOB-", page.Rows[0].Name, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failed_first_read_says_so_in_words_and_offers_Try_again()
    {
        _f.Server.JobsAnswer = RouteAnswer.Problem(System.Net.HttpStatusCode.ServiceUnavailable, "SND-API-011", "The views have not caught up");
        var page = _f.Page();
        page.SetShown(shown: true);

        await page.Refresh(Url, Token);

        Assert.Equal((false, true, 0), (page.IsReading, page.IsFailed, page.Rows.Count));
        Assert.StartsWith("Couldn't read the jobs just now.", page.FailedText, StringComparison.Ordinal);
        Assert.Equal("Try again", page.RetryText);
    }

    [Fact]
    public async Task Try_again_reads_the_list_and_clears_the_sentence()
    {
        _f.Server.JobsAnswer = RouteAnswer.Problem(System.Net.HttpStatusCode.ServiceUnavailable, "SND-API-011", "The views have not caught up");
        var page = _f.Page();
        page.SetShown(shown: true);
        await page.Refresh(Url, Token);
        _f.Server.JobsAnswer = RouteAnswer.Api("jobs", "running");

        await page.Retry(Token);

        Assert.Equal((false, 1), (page.IsFailed, page.Rows.Count));
    }

    [Fact]
    public async Task The_catch_up_read_runs_on_the_pages_token_which_is_cancelled_when_the_page_is_left()
    {
        var page = _f.Page();
        page.SetShown(shown: true);
        await page.Refresh(Url, Token);
        page.SetShown(shown: false);
        var before = _f.Server.JobsCalls;

        page.SetShown(shown: true);
        await page.CatchUp;

        Assert.Equal(before + 1, _f.Server.JobsCalls);
        Assert.True(_f.Server.LastJobsToken.CanBeCanceled);
        page.SetShown(shown: false);
        Assert.True(_f.Server.LastJobsToken.IsCancellationRequested);
    }
}
