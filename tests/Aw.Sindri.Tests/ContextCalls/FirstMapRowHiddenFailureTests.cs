using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>Doctor's first-map row keeps a failed build in view (#3216): a later map job that ended without building (cancelled) doesn't hide it; one that built does.</summary>
public sealed class FirstMapRowHiddenFailureTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly string[] _repos = ["demo"];
    private static readonly HashSet<string> _indexed = new(["demo"], StringComparer.Ordinal);

    private static JobRow Job(char id, JobState state, int endedMinutesAgo, string? code = null) => new(
        JobId.From("JOB-" + new string('0', 31) + id), JobKind.From("RefreshContextMap"), JobKey.From("map:all"), Item: null, Repo: null, Service: null, state,
        "service:context-map-refresh", _now.AddMinutes(-endedMinutesAgo - 1), _now.AddMinutes(-endedMinutesAgo), [], Rounds: 1,
        Summary: null, code, Result: null, CancelledBy: state == JobState.Cancelled ? "human:cli" : null, Sequence: id);

    [Fact, Trait("Lane", "unit")]
    public void A_cancelled_map_job_after_a_failed_build_keeps_the_failure_on_the_row()
    {
        var row = FirstMapRow.Of(_repos, paused: false, [Job('1', JobState.Failed, endedMinutesAgo: 10, code: "SND-CONTEXT-031"), Job('2', JobState.Cancelled, endedMinutesAgo: 1)], _indexed);

        Assert.Equal((CheckState.Warn, "no map yet: the first map build failed (SND-CONTEXT-031)"), (row.State, row.Detail));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_later_build_that_ended_clears_the_failure()
    {
        var row = FirstMapRow.Of(_repos, paused: false, [Job('1', JobState.Failed, endedMinutesAgo: 10, code: "SND-CONTEXT-031"), Job('2', JobState.Succeeded, endedMinutesAgo: 1)], _indexed);

        Assert.Equal(CheckState.Ok, row.State);
    }
}
