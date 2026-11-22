using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>
/// The whole map's 'building' member while the first code index build runs (#3015): no map job runs yet, but the map waits on that build, so the answer says building with
/// stage 'index' and the index build's own file count, never "no map yet" alone. A map build says stage 'map'.
/// </summary>
public sealed class WholeMapIndexStageTests
{
    private static readonly DateTimeOffset _started = new(2026, 11, 13, 6, 0, 0, TimeSpan.Zero);
    private static readonly Error _refusal = Context.Core.ContextErrors.NoMap.With("no map yet");
    private static readonly ContextScope _scope = new(Settings: null, ScopeRules.Usable([new ProfileRepoEntry("demo", "/work/demo")]), new ContextCaller(ContextCallerKind.Service, Repo: null, ContextClient.Service));
    private readonly InMemoryJobViews _jobs = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static JobRow Job(string kind, string key, char id, params string[] lines) => new(JobId.From("JOB-" + new string('0', 31) + id), JobKind.From(kind), JobKey.From(key), Item: null, Repo: null,
        Service: null, JobState.Running, "service:code-index-main-move", _started, EndedAt: null, [.. lines.Select((l, i) => new JobLine(Round: 1, l, _started.AddSeconds(i)))], Rounds: 1,
        Summary: null, Code: null, Result: null, CancelledBy: null, Sequence: 1);

    private async Task<JsonElement?> Building()
    {
        var error = await new WholeMapNotReady(_jobs, new InMemoryViewStore(SliceHarness.Json), SourcesEngineProxy.Create()).Explain(_refusal, _scope, Ct);
        return error.Extensions is { } extensions && extensions.Any(static p => p.Key == "building") ? extensions.First(static p => p.Key == "building").Value : null;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_first_index_build_of_a_mapped_repo_reads_as_building_at_stage_index_with_its_files()
    {
        _jobs.Put(Job("BuildCodeIndex", "index:demo", '7', "structure 120 of 400 files"));

        var building = await Building();

        Assert.NotNull(building);
        Assert.Equal("index", building.Value.GetProperty("stage").GetString());
        Assert.Equal((120, 400), (building.Value.GetProperty("filesDone").GetInt32(), building.Value.GetProperty("filesTotal").GetInt32()));
        Assert.Equal("JOB-0000000000000000000000000000007", building.Value.GetProperty("jobId").GetString());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_index_build_of_a_repo_the_map_does_not_cover_is_no_building()
    {
        _jobs.Put(Job("BuildCodeIndex", "index:elsewhere", '7', "structure 120 of 400 files"));

        Assert.Null(await Building());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_map_build_reads_as_stage_map_and_wins_over_an_index_build()
    {
        _jobs.Put(Job("BuildCodeIndex", "index:demo", '7', "structure 120 of 400 files"));
        _jobs.Put(Job("BuildContextMap", "map:all", '8', "extracting 50/412 files"));

        var building = await Building();

        Assert.NotNull(building);
        Assert.Equal("map", building.Value.GetProperty("stage").GetString());
        Assert.Equal((50, 412), (building.Value.GetProperty("filesDone").GetInt32(), building.Value.GetProperty("filesTotal").GetInt32()));
    }
}
