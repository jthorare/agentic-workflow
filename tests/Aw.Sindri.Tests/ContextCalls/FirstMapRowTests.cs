using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>Doctor's 'context:map' row before the first map (#3216): it says what the map waits on, and a fix only when the person has to act.</summary>
public sealed class FirstMapRowTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly string[] _repos = ["demo", "other"];
    private static int _next;

    private static JobRow Job(string kind, JobState state, int endedMinutesAgo = 1, string? code = null) => new(
        JobId.From("JOB-" + (++_next).ToString("x32", System.Globalization.CultureInfo.InvariantCulture)), JobKind.From(kind), JobKey.From("map:all"), Item: null, Repo: null, Service: null, state,
        "service:context-map-refresh", _now.AddMinutes(-endedMinutesAgo - 1), state is JobState.Running or JobState.Waiting ? null : _now.AddMinutes(-endedMinutesAgo), [], Rounds: 1,
        Summary: null, code, Result: null, CancelledBy: null, Sequence: _next);

    private static HashSet<string> None => new(StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void Before_any_code_index_it_names_the_repos_the_map_waits_on_and_gives_no_fix()
    {
        var row = FirstMapRow.Of(_repos, paused: false, [], None);

        Assert.Equal(("context:map", CheckState.Ok, "no map yet: the first map builds after the code index of demo, other", null), (row.Name, row.State, row.Detail, row.Fix));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_running_map_build_reads_building_the_first_map()
    {
        var row = FirstMapRow.Of(_repos, paused: false, [Job("RefreshContextMap", JobState.Running)], new HashSet<string>(["demo"], StringComparer.Ordinal));

        Assert.Equal((CheckState.Ok, "building the first map", null), (row.State, row.Detail, row.Fix));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_paused_map_service_warns_with_the_resume_fix()
    {
        var row = FirstMapRow.Of(_repos, paused: true, [], None);

        Assert.Equal(CheckState.Warn, row.State);
        Assert.Equal("sindri services resume context-map-refresh", row.Fix);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_failed_newest_map_build_warns_with_the_build_map_fix()
    {
        var jobs = new[] { Job("BuildContextMap", JobState.Failed, endedMinutesAgo: 2, code: "SND-CONTEXT-040"), Job("RefreshContextMap", JobState.Succeeded, endedMinutesAgo: 30) };

        var row = FirstMapRow.Of(_repos, paused: false, jobs, new HashSet<string>(["demo"], StringComparer.Ordinal));

        Assert.Equal(CheckState.Warn, row.State);
        Assert.Contains("SND-CONTEXT-040", row.Detail, StringComparison.Ordinal);
        Assert.StartsWith("sindri context map build", row.Fix, StringComparison.Ordinal);
        Assert.DoesNotContain("import", row.Fix, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_indexed_repo_with_no_map_job_yet_waits_for_the_next_tick()
    {
        var row = FirstMapRow.Of(_repos, paused: false, [], new HashSet<string>(["other"], StringComparer.Ordinal));

        Assert.Equal((CheckState.Ok, "no map yet: the first map builds on context-map-refresh's next tick", null), (row.State, row.Detail, row.Fix));
    }
}
