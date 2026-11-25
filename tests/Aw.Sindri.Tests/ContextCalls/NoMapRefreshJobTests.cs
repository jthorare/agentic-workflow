using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>
/// The first map is built by the context-map-refresh service's RefreshContextMap job, not a BuildContextMap job (#3595): the whole map's and the concepts' "no map yet"
/// answers must read that job as a build under way.
/// </summary>
public sealed class NoMapRefreshJobTests
{
    private static readonly DateTimeOffset _started = new(2026, 11, 13, 6, 0, 0, TimeSpan.Zero);
    private static readonly Error _refusal = Context.Core.ContextErrors.NoMap.With("no map yet");
    private static readonly ContextScope _scope = new(Settings: null, [], new ContextCaller(ContextCallerKind.Service, Repo: null, ContextClient.Service));
    private readonly InMemoryJobViews _jobs = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private void Put(string kind, JobState state) => _jobs.Put(new JobRow(JobId.From("JOB-" + new string('0', 31) + (kind == "RefreshContextMap" ? "9" : "8")), JobKind.From(kind), JobKey.From("map:all"), Item: null, Repo: null,
        Service: null, state, "service:context-map-refresh", _started, state == JobState.Running ? null : _started.AddMinutes(1), [], Rounds: 1,
        Summary: null, Code: null, Result: null, CancelledBy: null, Sequence: 1));

    private async Task<Error> Whole() =>
        await new WholeMapNotReady(_jobs, new InMemoryViewStore(SliceHarness.Json), SourcesEngineProxy.Create()).Explain(_refusal, _scope, Ct);

    private async Task<ConceptsView> Concepts() => await new ConceptsNotReady(_jobs).Explain(_refusal, _scope, Ct);

    private static string? Extension(Error error, string name) =>
        error.Extensions is { } extensions && extensions.Any(p => p.Key == name) ? extensions.First(p => p.Key == name).Value.GetRawText() : null;

    [Fact, Trait("Lane", "unit")]
    public async Task Whole_map_says_building_while_the_first_maps_refresh_job_runs()
    {
        Put("RefreshContextMap", JobState.Running);

        var error = await Whole();

        Assert.NotNull(Extension(error, "building"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Whole_map_names_the_refresh_jobs_start_as_the_first_build()
    {
        Put("RefreshContextMap", JobState.Running);

        var error = await Whole();

        Assert.Contains("2026-11-13T06:00:00", Extension(error, "noMap"), StringComparison.Ordinal);
        Assert.Contains("firstRunStartedAt", Extension(error, "noMap"), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Concepts_say_building_while_the_first_maps_refresh_job_runs()
    {
        Put("RefreshContextMap", JobState.Running);

        var view = await Concepts();

        Assert.Equal(ConceptStates.Building, view.State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Whole_map_has_no_building_member_when_no_map_job_runs()
    {
        Put("RefreshContextMap", JobState.Succeeded);
        Put("BuildContextMap", JobState.Succeeded);

        var error = await Whole();

        Assert.Null(Extension(error, "building"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Concepts_say_unavailable_when_no_map_job_runs()
    {
        var view = await Concepts();

        Assert.Equal(ConceptStates.Unavailable, view.State);
    }
}
