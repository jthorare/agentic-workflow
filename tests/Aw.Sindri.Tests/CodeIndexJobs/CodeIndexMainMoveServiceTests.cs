using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.CodeIndexJobs;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Tests.CodeIndexJobs;

/// <summary>The 'code-index-main-move' service (code index spec §6.1): per repo, a build starts when the checkout moved along its default branch past the stored index.</summary>
public sealed class CodeIndexMainMoveServiceTests
{
    private static readonly TimeSpan _minute = TimeSpan.FromMinutes(1);
    private readonly CodeIndexJobsRig _rig = new();
    private readonly Starter _start;
    private Core.Lease.LeaseGrant? _grant;

    public CodeIndexMainMoveServiceTests()
    {
        _start = new Starter(_rig.Harness);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Result<ScheduledResult>> Run()
    {
        if (_grant is null)
        {
            await _rig.Approve(Ct);   // once: Approve sets the clock to the rig's start, and a later run must not move it back
            _grant = await _rig.Harness.HoldLease();
        }

        var service = new CodeIndexMainMoveService(_rig.Scopes, _rig.Index, _rig.Heads, _rig.Jobs, _start, _rig.Harness.Clock, new SilentLogger());
        using var caller = _rig.Harness.Callers.EnterService("code-index-main-move");
        return await service.Run(new ScheduledRun(_grant, _rig.Harness.Clock.GetUtcNow()), Ct);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_head_past_the_stored_index_starts_one_build_of_that_commit_as_the_service()
    {
        _ = _rig.Index.WithIndex(CodeIndexJobsRig.Demo, CodeIndexJobsRig.Old);
        _ = _rig.Heads.With(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New, onDefaultBranch: true);

        var result = await Run();

        Assert.False(result.Value.Idle);
        Assert.Equal([new BuildCodeIndexRequest("demo", CodeIndexJobsRig.New.Value)], _start.Requests);
        Assert.Equal(["service:code-index-main-move"], _start.Actors);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_added_to_the_live_profile_only_is_built_on_the_next_tick_owner_q102_CIX_L59()
    {
        await _rig.Approve(Ct);   // seeds the clock and the profile; the live edit below replaces it, so Run must not seed again
        _grant = await _rig.Harness.HoldLease();
        var live = new SindriProfile(1, "thor", Repos: [new RepoEntry("demo", "/work/demo"), new RepoEntry("fresh", "/work/fresh")]);
        _rig.Harness.Profiles.Live = new LoadedProfile(live, ProfileHash.From(new string('a', 64)), "/home/profile.yaml");
        _ = _rig.Heads.With(CodeIndexJobsRig.Fresh, CodeIndexJobsRig.New, onDefaultBranch: true);

        _ = await Run();

        Assert.Equal([new BuildCodeIndexRequest("fresh", CodeIndexJobsRig.New.Value)], _start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_with_no_index_yet_starts_its_first_build()
    {
        _ = _rig.Heads.With(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New, onDefaultBranch: true);

        _ = await Run();

        Assert.Equal([new BuildCodeIndexRequest("demo", CodeIndexJobsRig.New.Value)], _start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_index_at_the_head_is_quiet_with_no_start()
    {
        _ = _rig.Index.WithIndex(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New);
        _ = _rig.Heads.With(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New, onDefaultBranch: true);

        var result = await Run();

        Assert.True(result.Value.Idle);
        Assert.Empty(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_checkout_on_a_feature_branch_starts_nothing_and_is_named()
    {
        _ = _rig.Heads.With(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New, onDefaultBranch: false);

        var result = await Run();

        Assert.Empty(_start.Requests);
        Assert.Contains("demo", result.Value.Summary, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Each_repo_is_decided_on_its_own()
    {
        _ = _rig.Index.WithIndex(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New).WithIndex(CodeIndexJobsRig.Other, CodeIndexJobsRig.Old);
        _ = _rig.Heads.With(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New, onDefaultBranch: true).With(CodeIndexJobsRig.Other, CodeIndexJobsRig.New, onDefaultBranch: true);

        _ = await Run();

        Assert.Equal(["other"], [.. _start.Requests.Select(static r => r.Repo)], StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_running_build_of_the_repo_holds_the_start_and_another_repos_does_not()
    {
        _ = _rig.Heads.With(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New, onDefaultBranch: true);
        _rig.Job("other", JobState.Running, TimeSpan.Zero);
        _ = await Run();
        _ = Assert.Single(_start.Requests);

        _rig.Job("demo", JobState.Running, TimeSpan.Zero);
        _start.Requests.Clear();
        _ = await Run();

        Assert.Empty(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_build_backs_off_and_the_third_in_a_row_fails_the_run()
    {
        _ = _rig.Heads.With(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New, onDefaultBranch: true);
        _rig.Job("demo", JobState.Failed, _minute);
        var backedOff = await Run();
        Assert.Empty(_start.Requests);
        Assert.True(backedOff.IsSuccess);

        _rig.Job("demo", JobState.Failed, TimeSpan.FromSeconds(50));
        _rig.Job("demo", JobState.Failed, TimeSpan.FromSeconds(40));
        var alarm = await Run();

        Assert.Equal(SindriErrors.ServiceFailed.Code, alarm.Error.Code);
        Assert.Empty(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_layer_that_became_buildable_starts_a_layers_only_build_at_the_same_commit()
    {
        _ = _rig.Index.WithIndex(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New).WithGraphUnavailable(CodeIndexJobsRig.Demo).WithGraphBuildable(CodeIndexJobsRig.Demo);
        _ = _rig.Heads.With(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New, onDefaultBranch: true);
        _rig.Job("demo", JobState.Succeeded, TimeSpan.FromMinutes(30), outcome: "published");

        _ = await Run();

        Assert.Equal([new BuildCodeIndexRequest("demo", CodeIndexJobsRig.New.Value)], _start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_layer_that_is_still_unavailable_starts_nothing()
    {
        _ = _rig.Index.WithIndex(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New).WithGraphUnavailable(CodeIndexJobsRig.Demo);
        _ = _rig.Heads.With(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New, onDefaultBranch: true);
        _rig.Job("demo", JobState.Succeeded, TimeSpan.FromMinutes(30), outcome: "published");

        _ = await Run();

        Assert.Empty(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_layers_only_build_that_just_ended_is_not_started_again_within_ten_minutes()
    {
        _ = _rig.Index.WithIndex(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New).WithGraphUnavailable(CodeIndexJobsRig.Demo).WithGraphBuildable(CodeIndexJobsRig.Demo);
        _ = _rig.Heads.With(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New, onDefaultBranch: true);
        _rig.Job("demo", JobState.Succeeded, TimeSpan.FromMinutes(4), outcome: "published");

        _ = await Run();

        Assert.Empty(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_probe_is_not_made_while_the_newest_build_ended_under_ten_minutes_ago()
    {
        _ = _rig.Index.WithIndex(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New).WithGraphUnavailable(CodeIndexJobsRig.Demo).WithGraphBuildable(CodeIndexJobsRig.Demo);
        _ = _rig.Heads.With(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New, onDefaultBranch: true);
        _rig.Job("demo", JobState.Succeeded, TimeSpan.FromMinutes(4), outcome: "published");

        _ = await Run();

        Assert.Equal(0, _rig.Index.ProbeCalls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_probe_is_not_made_when_no_layer_is_unavailable()
    {
        _ = _rig.Index.WithIndex(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New);
        _ = _rig.Heads.With(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New, onDefaultBranch: true);
        _rig.Job("demo", JobState.Succeeded, TimeSpan.FromMinutes(30), outcome: "published");

        _ = await Run();

        Assert.Equal(0, _rig.Index.ProbeCalls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_409_with_the_running_job_is_quiet()
    {
        _ = _rig.Heads.With(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New, onDefaultBranch: true);
        _start.Answer = SindriErrors.JobConflict.With("running").WithExtension("runningJob", System.Text.Json.JsonDocument.Parse("\"j1\"").RootElement.Clone());

        var result = await Run();

        Assert.True(result.IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_an_approved_profile_the_run_is_quiet()
    {
        var service = new CodeIndexMainMoveService(_rig.Scopes, _rig.Index, _rig.Heads, _rig.Jobs, _start, _rig.Harness.Clock, new SilentLogger());
        _grant ??= await _rig.Harness.HoldLease();

        var result = await service.Run(new ScheduledRun(_grant, CodeIndexJobsRig.Now), Ct);

        Assert.True(result.Value.Idle);
    }

    private sealed class SilentLogger : ILogger<CodeIndexMainMoveService>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }

    private sealed class Starter(SliceHarness harness) : IService<BuildCodeIndexRequest, JobStartedResponse>
    {
        internal List<BuildCodeIndexRequest> Requests { get; } = [];

        internal List<string> Actors { get; } = [];

        internal Error? Answer { get; set; }

        public ValueTask<Result<JobStartedResponse>> Handle(BuildCodeIndexRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Actors.Add(harness.Callers.Current?.Actor ?? string.Empty);
            return ValueTask.FromResult<Result<JobStartedResponse>>(Answer is { } error
                ? error
                : new JobStartedResponse(new CommandAccepted(harness.Ids.New<CommandId>(), 1), JobId.New(harness.Ids), new JobLinks("/j", "/c", "/e")));
        }
    }
}
