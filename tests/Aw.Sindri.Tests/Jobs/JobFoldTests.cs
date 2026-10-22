using System.Globalization;
using System.Text.Json;
using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Redaction;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Infrastructure.Text;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>The pure folds of a job's events into its row (API P6 unit A): the lines, the terminal states and the first-terminal-wins rule (D5).</summary>
[Lane(TestLane.Unit)]
public sealed class JobFoldTests
{
    private static readonly JobKind _kind = JobKind.From("RunService");
    private static readonly JobKey _key = JobKey.From("heartbeat");
    private readonly SliceHarness _h = new();
    private long _sequence;

    private CommandId Command => _h.Ids.New<CommandId>();

    private LedgerEvent Event<TPayload>(string type, TPayload payload)
    {
        _h.Clock.Advance(TimeSpan.FromSeconds(1));
        return Recorded.Of(_h.Events, type, payload, ++_sequence);
    }

    private JobRow Started()
    {
        var recorded = Event(EventTypes.JobStarted, new JobStarted(Command, A.FirstJobId, _kind, _key, Item: null, Repo: null, ServiceName.From("heartbeat")));
        return JobsProjection.Fold(row: null, _h.Events.Payload<JobStarted>(recorded).Value, recorded);
    }

    private JobRow Progress(JobRow row, int round, string line)
    {
        var recorded = Event(EventTypes.JobProgress, new JobProgress(Command, A.FirstJobId, round, line));
        return JobsProjection.Fold(row, _h.Events.Payload<JobProgress>(recorded).Value, recorded);
    }

    private JobRow Finished(JobRow row, JobOutcome outcome, string summary, string? code = null, JsonElement? result = null)
    {
        var recorded = Event(EventTypes.JobFinished, new JobFinished(Command, A.FirstJobId, outcome, summary, code, result));
        return JobsProjection.Fold(row, _h.Events.Payload<JobFinished>(recorded).Value, recorded);
    }

    private JobRow Cancelled(JobRow row, string by)
    {
        var recorded = Event(EventTypes.JobCancelled, new JobCancelled(Command, A.FirstJobId, by));
        return JobsProjection.Fold(row, _h.Events.Payload<JobCancelled>(recorded).Value, recorded);
    }

    private JobRow Waiting(JobRow row)
    {
        var recorded = Event(EventTypes.JobWaiting, new JobWaiting(Command, A.FirstJobId, "onyx-account"));
        return JobsProjection.Fold(row, _h.Events.Payload<JobWaiting>(recorded).Value, recorded);
    }

    private JobRow Resumed(JobRow row)
    {
        var recorded = Event(EventTypes.JobResumed, new JobResumed(Command, A.FirstJobId, "onyx-account"));
        return JobsProjection.Fold(row, _h.Events.Payload<JobResumed>(recorded).Value, recorded);
    }

    [Fact]
    public void A_waiting_job_is_waiting_and_resumes_to_running()
    {
        var waiting = Waiting(Started());

        Expect.Equal(JobState.Waiting, waiting.State);
        Expect.Equal(JobState.Running, Resumed(waiting).State);
    }

    [Fact]
    public void A_waiting_job_still_takes_progress_and_each_way_to_end()
    {
        var waiting = Waiting(Started());

        Expect.Equal(1, Progress(waiting, 1, "needs you").Rounds);
        Expect.Equal(JobState.Failed, Finished(waiting, JobOutcome.Failed, "ran out", "SND-SETUP-006").State);
        Expect.Equal(JobState.Succeeded, Finished(waiting, JobOutcome.Succeeded, "ok").State);
        Expect.Equal(JobState.Cancelled, Cancelled(waiting, "human:cli").State);
    }

    [Fact]
    public void A_wait_or_a_resume_on_an_ended_job_changes_nothing()
    {
        var ended = Finished(Started(), JobOutcome.Succeeded, "ok");

        Expect.Equal(JobState.Succeeded, Waiting(ended).State);
        Expect.Equal(JobState.Succeeded, Resumed(ended).State);
        Expect.Equal(JobState.Running, Resumed(Started()).State);   // not waiting: nothing to resume
    }

    [Fact]
    public void Started_makes_a_running_row()
    {
        var row = Started();

        Expect.Equal(JobState.Running, row.State);
        Expect.Equal((A.FirstJobId, _kind, _key), (row.Id, row.Kind, row.Key));
        Expect.Equal(ServiceName.From("heartbeat"), row.Service);
        Expect.Equal(0, row.Rounds);
        Assert.Empty(row.Progress);
        Assert.Null(row.EndedAt);
        Expect.Equal(1L, row.Sequence);
    }

    [Fact]
    public void A_raw_control_sequence_in_a_line_or_a_summary_is_escaped_on_read()
    {
        var row = Progress(Started(), 1, "\u001b[31mred\u001b[0m \u001b]0;title\u0007done");

        Expect.Equal("\\x1B[31mred\\x1B[0m \\x1B]0;title\\x07done", row.Progress[0].Line);
        Expect.Equal("ok \\x1B[2J", Finished(row, JobOutcome.Succeeded, "ok \u001b[2J").Summary);
    }

    [Fact]
    public void A_line_that_is_already_escaped_stays_as_it_is()
    {
        var row = Progress(Started(), 1, "\\x1B[31mred");

        Expect.Equal("\\x1B[31mred", row.Progress[0].Line);
    }

    [Fact]
    public void A_replayed_start_changes_nothing()
    {
        var row = Progress(Started(), 1, "one");
        var again = Event(EventTypes.JobStarted, new JobStarted(Command, A.FirstJobId, _kind, _key, Item: null, Repo: null, Service: null));

        Expect.Equal(row, JobsProjection.Fold(row, _h.Events.Payload<JobStarted>(again).Value, again));
    }

    [Fact]
    public void Fold_keeps_the_last_200_lines()
    {
        var row = Started();
        for (var n = 1; n <= 250; n++)
        {
            row = Progress(row, n, string.Create(CultureInfo.InvariantCulture, $"line {n}"));
        }

        Expect.Equal(JobLimits.ViewLines, row.Progress.Count);
        Expect.Equal(51, row.Progress[0].Round);
        Expect.Equal(250, row.Progress[^1].Round);
        Expect.Equal(250, row.Rounds);
    }

    [Fact]
    public void Rounds_are_the_highest_round_seen()
    {
        var row = Progress(Progress(Started(), 5, "five"), 3, "late three");

        Expect.Equal(5, row.Rounds);
    }

    [Fact]
    public void Finished_succeeded_ends_the_row_with_its_summary_and_result()
    {
        var result = JsonSerializer.SerializeToElement(new { lines = 3 });

        var row = Finished(Progress(Started(), 1, "one"), JobOutcome.Succeeded, "echoed 3", code: null, result);

        Expect.Equal(JobState.Succeeded, row.State);
        Expect.Equal("echoed 3", row.Summary);
        Expect.Equal(3, row.Result!.Value.GetProperty("lines").GetInt32());
        Expect.True(row.EndedAt is not null);
        Assert.Null(row.Code);
    }

    [Fact]
    public void Finished_failed_keeps_the_code()
    {
        var row = Finished(Started(), JobOutcome.Failed, "boom", "SND-SCHED-001");

        Expect.Equal((JobState.Failed, "SND-SCHED-001", "boom"), (row.State, row.Code, row.Summary));
        Assert.Null(row.Result);
    }

    [Fact]
    public void Cancelled_ends_the_row_and_names_who_cancelled()
    {
        var row = Cancelled(Started(), "human:ui");

        Expect.Equal((JobState.Cancelled, "human:ui"), (row.State, row.CancelledBy));
        Expect.True(row.EndedAt is not null);
    }

    [Fact]
    public void First_terminal_event_wins()
    {
        var finished = Finished(Started(), JobOutcome.Succeeded, "done");
        var cancelledAfter = Cancelled(finished, "human:ui");
        var cancelled = Cancelled(Started(), "human:ui");
        var finishedAfter = Finished(cancelled, JobOutcome.Failed, "late", "SND-JOB-001");

        Expect.Equal(finished, cancelledAfter);
        Expect.Equal(cancelled, finishedAfter);
        Expect.Equal(JobState.Succeeded, cancelledAfter.State);
        Expect.Equal(JobState.Cancelled, finishedAfter.State);
    }

    [Fact]
    public void Progress_after_the_end_is_ignored()
    {
        var finished = Finished(Progress(Started(), 1, "one"), JobOutcome.Succeeded, "done");

        Expect.Equal(finished, Progress(finished, 2, "too late"));
    }

    [Fact]
    public void Ansi_osc_and_a_secret_in_a_line_arrive_escaped_and_scrubbed()
    {
        var secret = "ghp_" + new string('a', 36);
        var scrubber = new RedactingScrubber(Redactor.Default);

        var cleaned = JobLines.Clean(scrubber, $"\u001b]0;title\u0007\u001b[31mtoken {secret}\u001b[0m");

        Assert.DoesNotContain(secret, cleaned, StringComparison.Ordinal);
        Assert.DoesNotContain('\u001b', cleaned);
        Assert.DoesNotContain('\u0007', cleaned);
        Assert.Contains("\\x1B", cleaned, StringComparison.Ordinal);
    }
}
