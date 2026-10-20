using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The 'context-map-refresh' service (owner F1): it starts a refresh when a mapped repo's trunk moved, over the jobs view in memory.</summary>
public sealed class ContextMapRefreshServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly JobKind _kind = JobKind.From("RefreshContextMap");
    private static readonly JsonSerializerOptions _web = new(JsonSerializerDefaults.Web);
    private readonly SliceHarness _h = new();
    private readonly InMemoryJobViews _jobs = new();
    private readonly Starter _start;
    private readonly HeadsEngineProxy _engine = HeadsEngineProxy.Create();
    private Core.Lease.LeaseGrant? _grant;
    private int _next;

    public ContextMapRefreshServiceTests()
    {
        _start = new Starter(_h);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static RepoHead Head(string repo, string sha, bool onDefault = true) => new(ContextRepoName.From(repo), sha, onDefault);

    private async Task<ContextMapRefreshService> Service(bool context = true)
    {
        _h.Clock.SetUtcNow(_now);
        var loaded = SliceHarness.Profile("5s");
        loaded = loaded with { Profile = loaded.Profile with { Context = context ? JsonDocument.Parse("""{"docs":{"globs":["docs/**/*.md"]}}""").RootElement.Clone() : null } };
        _ = await _h.Profiles.Save(loaded, Ct);
        return new ContextMapRefreshService(new ContextScopes(_h.Profiles, _h.Callers), _engine.Proxy, _jobs, _start, _h.Clock, new CapturingLogger());
    }

    private void Job(JobState state, TimeSpan endedAgo, params (string Repo, string Sha)[] heads)
    {
        _next++;
        var result = state == JobState.Succeeded
            ? JsonSerializer.SerializeToElement(new RefreshContextMapResult("r", 1, heads.ToDictionary(static h => h.Repo, static h => h.Sha, StringComparer.Ordinal), 0, 0, 0, "none"), _web)
            : (JsonElement?)null;
        _jobs.Put(new JobRow(JobId.New(_h.Ids), _kind, JobKey.From("refresh"), Item: null, Repo: null, Service: null, state,
            "service:context-map-refresh", _now - endedAgo - TimeSpan.FromSeconds(5), state == JobState.Running ? null : _now - endedAgo, [], Rounds: 0, Summary: null,
            state == JobState.Failed ? "SND-CONTEXT-030" : null, result, CancelledBy: null, Sequence: _next));
    }

    private async Task<Result<ScheduledResult>> Run(ContextMapRefreshService service)
    {
        _grant ??= await _h.HoldLease();
        using var caller = _h.Callers.EnterService("context-map-refresh");
        return await service.Run(new ScheduledRun(_grant, _now), Ct);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Moved_heads_start_one_refresh_as_the_service()
    {
        Job(JobState.Succeeded, TimeSpan.FromHours(1), ("a", "1"));
        _engine.Heads = [Head("a", "2")];

        var result = await Run(await Service());

        Assert.False(result.Value.Idle);
        _ = Assert.Single(_start.Requests);
        Assert.Equal("service:context-map-refresh", _start.Actors.Single());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_first_ever_run_starts()
    {
        _engine.Heads = [Head("a", "1")];

        var result = await Run(await Service());

        Assert.False(result.Value.Idle);
        _ = Assert.Single(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_same_heads_as_the_last_success_are_quiet_with_no_start()
    {
        Job(JobState.Succeeded, TimeSpan.FromHours(1), ("a", "1"), ("b", "2"));
        _engine.Heads = [Head("b", "2"), Head("a", "1")];

        var result = await Run(await Service());

        Assert.True(result.Value.Idle);
        Assert.Empty(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_feature_branch_head_is_left_out_and_named_in_the_summary()
    {
        Job(JobState.Succeeded, TimeSpan.FromHours(1), ("a", "1"));
        _engine.Heads = [Head("a", "1"), Head("b", "9", onDefault: false)];

        var result = await Run(await Service());

        Assert.True(result.Value.Idle);
        Assert.Contains("b", result.Value.Summary, StringComparison.Ordinal);
        Assert.Empty(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_moved_repo_starts_while_another_sits_on_a_feature_branch_and_the_summary_names_it()
    {
        Job(JobState.Succeeded, TimeSpan.FromHours(1), ("a", "1"));
        _engine.Heads = [Head("a", "2"), Head("b", "9", onDefault: false)];

        var result = await Run(await Service());

        Assert.False(result.Value.Idle);
        Assert.Contains("b", result.Value.Summary, StringComparison.Ordinal);
        _ = Assert.Single(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_running_refresh_is_never_doubled()
    {
        Job(JobState.Running, TimeSpan.Zero);
        _engine.Heads = [Head("a", "2")];

        var result = await Run(await Service());

        Assert.True(result.Value.Idle);
        Assert.Empty(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_refresh_backs_off_and_starts_again_when_the_wait_is_over()
    {
        Job(JobState.Failed, TimeSpan.FromMinutes(3));   // the first wait is 5 minutes
        _engine.Heads = [Head("a", "1")];
        var service = await Service();
        var waiting = await Run(service);
        _h.Clock.Advance(TimeSpan.FromMinutes(3));
        var elapsed = await Run(service);

        Assert.True(waiting.Value.Idle);
        Assert.False(elapsed.Value.Idle);
        _ = Assert.Single(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Three_failed_refreshes_in_a_row_fail_the_run_naming_the_service_even_while_backing_off()
    {
        Job(JobState.Failed, TimeSpan.FromMinutes(30));
        Job(JobState.Failed, TimeSpan.FromMinutes(20));
        Job(JobState.Failed, TimeSpan.FromMinutes(1));
        _engine.Heads = [Head("a", "1")];

        var result = await Run(await Service());

        Assert.True(result.IsError);
        Assert.Contains("context-map-refresh", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(SindriErrors.ServiceFailed.Code, result.Error.Code);
        Assert.Empty(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Three_failed_refreshes_start_again_when_the_wait_is_over_and_the_run_stays_failed_until_one_succeeds()
    {
        Job(JobState.Failed, TimeSpan.FromMinutes(50));
        Job(JobState.Failed, TimeSpan.FromMinutes(40));
        Job(JobState.Failed, TimeSpan.FromMinutes(25));   // the third wait is 20 minutes
        _engine.Heads = [Head("a", "1")];

        var result = await Run(await Service());

        _ = Assert.Single(_start.Requests);
        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.ServiceFailed.Code, result.Error.Code);
        Assert.Contains("started", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Three_failures_are_quiet_once_the_heads_are_back_at_the_last_mapped_ones()
    {
        Job(JobState.Succeeded, TimeSpan.FromMinutes(60), ("a", "1"));
        Job(JobState.Failed, TimeSpan.FromMinutes(30));
        Job(JobState.Failed, TimeSpan.FromMinutes(20));
        Job(JobState.Failed, TimeSpan.FromMinutes(1));
        _engine.Heads = [Head("a", "1")];

        var result = await Run(await Service());

        Assert.True(result.Value.Idle);
        Assert.Empty(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Two_failures_are_not_yet_a_failed_run_and_a_success_after_failures_is_a_clean_run()
    {
        Job(JobState.Failed, TimeSpan.FromMinutes(30));
        Job(JobState.Failed, TimeSpan.FromMinutes(20));
        _engine.Heads = [Head("a", "1")];
        var twoFailures = await Run(await Service());
        Job(JobState.Succeeded, TimeSpan.FromMinutes(1), ("a", "1"));
        var healed = await Run(await Service());

        Assert.False(twoFailures.IsError);
        Assert.True(healed.Value.Idle);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_context_section_no_approved_profile_and_no_repo_on_trunk_are_quiet()
    {
        _engine.Heads = [Head("a", "1")];
        Assert.True((await Run(await Service(context: false))).Value.Idle);
        _engine.Heads = [Head("a", "1", onDefault: false)];
        Assert.True((await Run(await Service())).Value.Idle);
        var unapproved = new ContextMapRefreshService(new ContextScopes(new SliceHarness().Profiles, _h.Callers), _engine.Proxy, _jobs, _start, _h.Clock, new CapturingLogger());
        Assert.True((await Run(unapproved)).Value.Idle);
        Assert.Empty(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_removed_repo_alone_does_not_start_a_refresh()
    {
        Job(JobState.Succeeded, TimeSpan.FromHours(1), ("a", "1"), ("b", "2"));
        _engine.Heads = [Head("a", "1")];

        var result = await Run(await Service());

        Assert.True(result.Value.Idle);
        Assert.Empty(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_same_refresh_already_running_is_quiet_and_any_other_refusal_fails()
    {
        _engine.Heads = [Head("a", "1")];
        var service = await Service();
        using var id = JsonDocument.Parse("\"JOB-0000000000000001\"");
        _start.Answer = SindriErrors.JobConflict.With("running").WithExtension("runningJob", id.RootElement.Clone());
        var same = await Run(service);
        _start.Answer = SindriErrors.JobsRateLimited.With("too many jobs");
        var refused = await Run(service);

        Assert.True(same.Value.Idle);
        Assert.Equal(SindriErrors.JobsRateLimited.Code, refused.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_service_is_a_built_in_every_minute() =>
        Assert.Equal(("context-map-refresh", TimeSpan.FromMinutes(1)), (ContextMapRefreshService.BuiltIn.Name.Value, ContextMapRefreshService.BuiltIn.Cadence.Interval));

    private sealed class CapturingLogger : ILogger<ContextMapRefreshService>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }

    private sealed class Starter(SliceHarness harness) : IService<RefreshContextMapRequest, JobStartedResponse>
    {
        internal List<RefreshContextMapRequest> Requests { get; } = [];

        internal List<string> Actors { get; } = [];

        internal Error? Answer { get; set; }

        public ValueTask<Result<JobStartedResponse>> Handle(RefreshContextMapRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Actors.Add(harness.Callers.Current?.Actor ?? string.Empty);
            return ValueTask.FromResult<Result<JobStartedResponse>>(Answer is { } error
                ? error
                : new JobStartedResponse(new CommandAccepted(harness.Ids.New<CommandId>(), 1), JobId.New(harness.Ids), new JobLinks("/j", "/c", "/e")));
        }
    }
}
