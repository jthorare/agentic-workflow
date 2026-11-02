using System.Text.Json;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Host.Text;
using static Aw.Sindri.Tests.Commands.JobRig;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>What 'context map build' prints when its job ends: the draft line and each degraded reason (clusters included), so a lost cluster pass is never silent.</summary>
public sealed class MapBuildJobTextTests : IDisposable
{
    private static readonly string[] _reasons = ["clusters: timed out after 60 min", "labels"];
    private readonly Commands.JobRig _rig = new();

    public void Dispose() => _rig.Dispose();

    private static BuildContextMapResult Result(params string[] degraded) => new("web-app", 7, 42, degraded);

    [Fact, Trait("Lane", "unit")]
    public void A_build_with_degraded_parts_prints_one_line_each_with_the_reason()
    {
        Assert.Equal("draft 7 for web-app · 42 proposed\ndegraded: clusters: timed out after 60 min\ndegraded: labels\n", MapBuildJobText.Render(Result(_reasons)).ReplaceLineEndings("\n"));
        Assert.Equal("draft 7 for web-app · 42 proposed\n", MapBuildJobText.Render(Result()).ReplaceLineEndings("\n"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_followed_build_prints_the_reasons_after_its_succeeded_line()
    {
        var api = new Commands.JobApi().Ok("POST", "/sindri/context/map/build", Body(Started))
            .Ok("GET", "/sindri/jobs/" + Id, View(JobState.Succeeded, rounds: 1, summary: "draft 7", result: JsonSerializer.SerializeToElement(Result(_reasons), SliceHarness.Json)));
        var run = Task.Run(async () => await new ContextJobVerbs(Client(api), _rig.Output(), _rig.Follower(api)).Run(["map", "build"], _rig.Interrupted, _rig.Again));
        await api.Until(() => api.Streams == 1);
        await api.Frame(11, "job.finished", Finished("succeeded", "draft 7"));

        var exit = await Finished(run);

        Assert.Equal(0, exit);
        Assert.Contains("succeeded: draft 7\ndraft 7 for web-app · 42 proposed\ndegraded: clusters: timed out after 60 min\n", _rig.Out.ToString().ReplaceLineEndings("\n"), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Notes_and_the_migration_line_print_after_the_degraded_lines_and_before_the_checks_line()
    {
        var result = new BuildContextMapResult("web-app", 7, 42, ["labels"])
        {
            Notes = ["concepts: 3 pins matched no feature"],
            Replaced = new Context.Core.Map.StructureCounts(2_790, 111_044, 14, 3),
            Checks = "structure: 4% isolated (limit 30%), 0% over 150 files (limit 2%)",
        };

        Assert.Equal(
            "draft 7 for web-app · 42 proposed\ndegraded: labels\nnote: concepts: 3 pins matched no feature\nmigration: 2,790 model features and 111,044 model links replaced; 14 decisions had no target and were undone; 3 carried over\nstructure: 4% isolated (limit 30%), 0% over 150 files (limit 2%)\n",
            MapBuildJobText.Render(result).ReplaceLineEndings("\n"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_build_that_replaced_nothing_prints_no_migration_line()
    {
        var result = new BuildContextMapResult("web-app", 7, 42, []) { Replaced = Context.Core.Map.StructureCounts.None };

        Assert.Equal("draft 7 for web-app · 42 proposed\n", MapBuildJobText.Render(result).ReplaceLineEndings("\n"));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_result_of_a_report_carries_its_notes_and_counts_and_caps_the_notes_at_twenty()
    {
        var report = new Context.Core.Map.MapBuildReport(null!, [], ["clusters"], []) { Notes = [.. Enumerable.Range(0, 30).Select(static i => "n" + i.ToString(System.Globalization.CultureInfo.InvariantCulture))], Structure = new Context.Core.Map.StructureCounts(1, 2, 0, 0) };

        var result = BuildContextMapResult.Of("web-app", 7, 42, report);

        Assert.Equal((20, "n0", 1), (result.Notes.Count, result.Notes[0], result.Replaced?.Features));
        Assert.Equal(["clusters"], result.Degraded);
        Assert.Null(BuildContextMapResult.Of("web-app", 7, 42, report with { Structure = Context.Core.Map.StructureCounts.None, Notes = [] }).Replaced);
    }
}
