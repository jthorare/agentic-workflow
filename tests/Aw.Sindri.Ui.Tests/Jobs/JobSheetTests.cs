using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;
using static Aw.Sindri.Ui.Tests.Jobs.JobsFixture;

namespace Aw.Sindri.Ui.Tests.Jobs;

/// <summary>The job sheet (API P6): the job in words, its progress and result, Cancel while it runs, the technical fields under Details, and every way a read can end.</summary>
[Lane(TestLane.Unit)]
public sealed class JobSheetTests
{
    private readonly JobsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_running_job_reads_in_words_and_can_be_cancelled()
    {
        _f.ServeJob("running");
        var sheet = _f.Sheet();

        await sheet.Open(Url, Id, Token);

        Assert.Equal((FramePhase.Shown, "Echo job", "● Running · Started 5m ago · by Sindri"), (sheet.Phase, sheet.Heading, sheet.StateLine));
        Assert.Equal(("Cancel job", true, false), (sheet.Cancel?.Label, sheet.HasNoProgress, sheet.HasSummary));
    }

    [Fact]
    public async Task A_failed_job_says_how_and_offers_no_Cancel()
    {
        _f.ServeJob("failed");
        var sheet = _f.Sheet();

        await sheet.Open(Url, Id, Token);

        Assert.Equal(("✕ Failed · Ended 5m ago · by Sindri", "it broke", true), (sheet.StateLine, sheet.SummaryText, sheet.Cancel is null));
        Assert.Contains(sheet.Fields, f => f.Label == Labels.JobCodeField && f.Value == "SND-SCHED-001");
        Assert.True(sheet.IsFailedJob);
    }

    [Fact]
    public async Task The_progress_and_the_result_come_from_the_job()
    {
        _f.ServeJob("context-backtest");
        var sheet = _f.Sheet();

        await sheet.Open(Url, Id, Token);

        Assert.Equal(["corpus: 5 samples", "arm graph 5/5"], sheet.Progress, StringComparer.Ordinal);
        Assert.Equal((true, true), (sheet.HasProgress, sheet.HasResult));
        Assert.Contains("\"passLine\": \"owner decision needed (small sample)\"", sheet.ResultText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_job_with_no_result_has_no_disclosure_for_one()
    {
        _f.ServeJob("running");
        var sheet = _f.Sheet();

        await sheet.Open(Url, Id, Token);

        Assert.False(sheet.HasResult);
    }

    [Fact]
    public async Task The_id_and_the_kind_as_served_wait_under_Details()
    {
        _f.ServeJob("running");
        var sheet = _f.Sheet();

        await sheet.Open(Url, Id, Token);

        Assert.DoesNotContain(Id, sheet.Heading + sheet.StateLine + sheet.SummaryText, StringComparison.Ordinal);
        Assert.Equal(["Job id: " + Id, "Kind: EchoJob", "Key: echo", "Started by (as served): service:host", "Rounds: 0"], sheet.Fields.Select(f => f.Name), StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_cancel_in_a_terminal_is_there_to_copy_and_copies()
    {
        _f.ServeJob("running");
        var sheet = _f.Sheet();
        await sheet.Open(Url, Id, Token);

        await sheet.CopyCli(Token);

        Assert.Equal(("sindri jobs cancel " + Id, true, Labels.Copied), (_f.Clipboard.Text, sheet.HasCli, sheet.CopyLabel));
    }

    [Fact]
    public async Task A_job_Sindri_does_not_know_says_so_in_words()
    {
        var sheet = _f.Sheet();

        await sheet.Open(Url, Id, Token);

        Assert.Equal((FramePhase.NotFound, Labels.JobUnknown, true), (sheet.Phase, sheet.ProblemText, sheet.IsProblem));
    }

    [Fact]
    public async Task An_answer_this_build_cannot_read_is_a_plain_sentence()
    {
        _f.Server.JobAnswers[Id] = new RouteAnswer(System.Net.HttpStatusCode.OK, "[]");
        var sheet = _f.Sheet();

        await sheet.Open(Url, Id, Token);

        Assert.Equal((FramePhase.Unreadable, Labels.JobsUnreadable), (sheet.Phase, sheet.ProblemText));
    }

    [Fact]
    public async Task A_failed_re_read_keeps_the_job_and_adds_a_note()
    {
        _f.ServeJob("running");
        var sheet = _f.Sheet();
        await sheet.Open(Url, Id, Token);
        _f.Server.JobAnswers[Id] = RouteAnswer.Problem(System.Net.HttpStatusCode.ServiceUnavailable, "SND-API-011", "The views have not caught up");

        await sheet.Refresh(Url, Token);

        Assert.Equal((FramePhase.Shown, "Echo job"), (sheet.Phase, sheet.Heading));
        Assert.StartsWith("Couldn't read the job just now.", sheet.Note, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_re_read_shows_the_job_when_it_finishes()
    {
        _f.ServeJob("running");
        var sheet = _f.Sheet();
        await sheet.Open(Url, Id, Token);
        _f.ServeJob("result");

        await sheet.Refresh(Url, Token);

        Assert.Equal(("✓ Done · Ended 5m ago · by Sindri", "echoed 2", true), (sheet.StateLine, sheet.SummaryText, sheet.Cancel is null));
        Assert.Equal("{\n  \"lines\": 2\n}".ReplaceLineEndings("\n"), sheet.ResultText.ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task Showing_another_job_clears_the_first()
    {
        _f.ServeJob("failed");
        var sheet = _f.Sheet();
        await sheet.Open(Url, Id, Token);

        sheet.Show("JOB-2");

        Assert.Equal(("JOB-2", FramePhase.Loading, string.Empty, string.Empty, 0), (sheet.Id, sheet.Phase, sheet.Heading, sheet.SummaryText, sheet.Fields.Count));
    }

    [Fact]
    public async Task Refresh_before_any_job_is_named_reads_nothing()
    {
        var sheet = _f.Sheet();

        await sheet.Refresh(Url, Token);

        Assert.Equal(0, _f.Server.JobCalls);
    }

    [Fact]
    public async Task A_garbage_job_id_says_the_plain_unknown_sentence_with_the_code_in_Details()
    {
        _f.Server.JobAnswers[Id] = RouteAnswer.Problem("jobs-id", "bad-id");
        var sheet = _f.Sheet();

        await sheet.Open(Url, Id, Token);

        Assert.Equal((FramePhase.NotFound, Labels.JobUnknown), (sheet.Phase, sheet.ProblemText));
        Assert.Contains("SND-CLI-003", sheet.ProblemCode, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_job_keeps_its_code_out_of_the_sentence()
    {
        var sheet = _f.Sheet();

        await sheet.Open(Url, Id, Token);

        Assert.DoesNotContain("SND-", sheet.ProblemText, StringComparison.Ordinal);
        Assert.True(sheet.HasProblemCode);
    }

    [Fact]
    public async Task A_sheet_opened_before_the_catalog_arrived_gets_its_Cancel_when_it_does()
    {
        var f = new JobsFixture(catalog: false);
        f.ServeJob("running");
        var sheet = f.Sheet();
        await sheet.Open(Url, Id, Token);
        Assert.Null(sheet.Cancel);

        f.ShowCatalog();

        Assert.Equal("Cancel job", sheet.Cancel?.Label);
    }

    [Fact]
    public async Task Copied_does_not_carry_over_to_another_job()
    {
        _f.ServeJob("running");
        var sheet = _f.Sheet();
        await sheet.Open(Url, Id, Token);
        await sheet.CopyCli(Token);
        Assert.Equal(Labels.Copied, sheet.CopyLabel);

        sheet.Show("JOB-2");

        Assert.Equal(Labels.Copy, sheet.CopyLabel);
    }
}
