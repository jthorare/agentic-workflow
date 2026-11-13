using System.Text.Json;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The whole map's 'building' member counts the files of the build's newest "extracting n/N files" line (#3394).</summary>
public sealed class WholeMapBuildingTests
{
    private static readonly DateTimeOffset _started = new(2026, 11, 12, 6, 0, 0, TimeSpan.Zero);

    private static JobRow Build(params string[] lines) => new(JobId.From("JOB-" + new string('0', 31) + "8"), JobKind.From("BuildContextMap"), JobKey.From("map:all"), Item: null, Repo: null,
        Service: null, JobState.Running, "human", _started, EndedAt: null, [.. lines.Select((l, i) => new JobLine(Round: 1, l, _started.AddSeconds(i)))], Rounds: 1,
        Summary: null, Code: null, Result: null, CancelledBy: null, Sequence: 1);

    [Fact, Trait("Lane", "unit")]
    public void Building_counts_the_files_of_the_newest_extracting_line()
    {
        var building = WholeMapNotReady.Building(Build("extracting 50/412 files", "extracting 100/412 files", "labeling 9 features"), sources: null);

        Assert.Equal((100, 412), (building.GetProperty("filesDone").GetInt32(), building.GetProperty("filesTotal").GetInt32()));
    }

    [Fact, Trait("Lane", "unit")]
    public void Building_leaves_the_files_out_before_the_first_extracting_line()
    {
        var building = WholeMapNotReady.Building(Build("waiting for the running map build"), sources: null);

        Assert.False(building.TryGetProperty("filesDone", out _));
        Assert.False(building.TryGetProperty("filesTotal", out _));
        Assert.Equal(JsonValueKind.String, building.GetProperty("jobId").ValueKind);
    }
}
