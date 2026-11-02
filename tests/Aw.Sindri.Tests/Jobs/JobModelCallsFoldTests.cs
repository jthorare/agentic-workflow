using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>The jobs row keeps the job's last 200 model calls (context model routing spec §8.1) and counts the ones that fell back to a second harness.</summary>
[Lane(TestLane.Unit)]
public sealed class JobModelCallsFoldTests
{
    private readonly SliceHarness _h = new();
    private long _sequence;

    private JobRow Started()
    {
        _h.Clock.Advance(TimeSpan.FromSeconds(1));
        var started = Recorded.Of(_h.Events, EventTypes.JobStarted,
            new JobStarted(_h.Ids.New<CommandId>(), A.FirstJobId, JobKind.From("BuildContextMap"), JobKey.From("map"), Item: null, Repo: null, Service: null), ++_sequence);
        return JobsProjection.Fold(row: null, _h.Events.Payload<JobStarted>(started).Value, started);
    }

    private JobRow Called(JobRow row, ContextModelCalled called)
    {
        _h.Clock.Advance(TimeSpan.FromSeconds(1));
        var recorded = Recorded.Of(_h.Events, EventTypes.ContextModelCalled, called, ++_sequence);
        return JobsProjection.Fold(row, _h.Events.Payload<ContextModelCalled>(recorded).Value, recorded);
    }

    [Fact]
    public void A_job_that_made_no_call_keeps_no_call_list() => Assert.Null(Started().ModelCalls);

    [Fact]
    public void Each_call_is_kept_in_the_order_it_was_made()
    {
        var row = Called(Started(), ModelCallRows.Answered(A.FirstJobId, "call-1", "codex", "gpt-6-luna", 100, 50));
        row = Called(row, ModelCallRows.Failed(A.FirstJobId, "call-2"));

        Expect.Sequence(["call-1", "call-2"], row.ModelCalls!.Select(static c => c.CallId.Value));
    }

    [Fact]
    public void Only_the_last_200_calls_are_kept_while_the_totals_count_every_one()
    {
        var row = Started();
        for (var n = 1; n <= 205; n++)
        {
            row = Called(row, ModelCallRows.Answered(A.FirstJobId, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"call-{n}"), "codex", "gpt-6-luna", 1, 1));
        }

        Expect.Equal(200, row.ModelCalls!.Count);
        Expect.Equal(("call-6", "call-205"), (row.ModelCalls[0].CallId.Value, row.ModelCalls[^1].CallId.Value));
        Expect.Equal(205, row.Models!.Calls);
    }

    [Fact]
    public void A_call_a_later_harness_answered_after_an_earlier_one_limited_counts_as_fell_back()
    {
        var row = Called(Started(), ModelCallRows.Answered(A.FirstJobId, "call-1", "codex", "gpt-6-luna", 10, 5));
        row = Called(row, ModelCallRows.FellBack(A.FirstJobId, "call-2", limited: "claude-code", answered: "codex", model: "gpt-6-luna", input: 40, output: 8));
        row = Called(row, ModelCallRows.Failed(A.FirstJobId, "call-3"));

        Expect.Equal(1, row.Models!.FellBack);
    }

    [Fact]
    public void Folding_the_same_call_event_again_keeps_it_once()
    {
        var recorded = Recorded.Of(_h.Events, EventTypes.ContextModelCalled, ModelCallRows.Answered(A.FirstJobId, "call-1", "codex", "gpt-6-luna", 1, 1), sequence: 10);
        var payload = _h.Events.Payload<ContextModelCalled>(recorded).Value;
        var once = JobsProjection.Fold(Started(), payload, recorded);

        Expect.Equal(1, JobsProjection.Fold(once, payload, recorded).ModelCalls!.Count);
    }
}
