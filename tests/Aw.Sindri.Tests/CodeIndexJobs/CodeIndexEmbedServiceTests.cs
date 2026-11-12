using Aw.CodeIndex.Core.Model;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.CodeIndexJobs;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Tests.CodeIndexJobs;

/// <summary>The 'code-index-embed' service (code index spec §6.3): per repo, an embed job starts while the index's embeddings layer is pending, with no HEAD move.</summary>
public sealed class CodeIndexEmbedServiceTests
{
    private static readonly TimeSpan _minute = TimeSpan.FromMinutes(1);
    private readonly CodeIndexJobsRig _rig = new();
    private readonly Starter _start;
    private Core.Lease.LeaseGrant? _grant;

    public CodeIndexEmbedServiceTests()
    {
        _start = new Starter(_rig.Harness);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Result<ScheduledResult>> Run()
    {
        if (_grant is null)
        {
            await _rig.Approve(Ct);
            _grant = await _rig.Harness.HoldLease();
        }

        var service = new CodeIndexEmbedService(_rig.Scopes, _rig.Index, _rig.Jobs, _start, _rig.Harness.Clock, new SilentLogger());
        using var caller = _rig.Harness.Callers.EnterService("code-index-embed");
        return await service.Run(new ScheduledRun(_grant, _rig.Harness.Clock.GetUtcNow()), Ct);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pending_embeddings_layer_starts_one_embed_of_that_repo_as_the_service_without_any_head_move()
    {
        _ = _rig.Index.WithIndex(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New).WithEmbeddings(CodeIndexJobsRig.Demo, LayerState.Pending);

        var result = await Run();

        Assert.False(result.Value.Idle);
        Assert.Equal([new EmbedCodeIndexRequest("demo")], _start.Requests);
        Assert.Equal(["service:code-index-embed"], _start.Actors);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_embeddings_layer_left_unavailable_by_a_server_failure_starts_an_embed_again_gh_3069()
    {
        _ = _rig.Index.WithIndex(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New).WithEmbeddings(CodeIndexJobsRig.Demo, LayerState.Unavailable, code: "SND-CODEIDX-006");
        _rig.EmbedJob("demo", JobState.Succeeded, _minute, EmbedCodeIndexResult.Pending, embedded: 8_576);

        var result = await Run();

        Assert.False(result.Value.Idle);
        Assert.Equal([new EmbedCodeIndexRequest("demo")], _start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_server_failure_layer_whose_last_retry_made_no_progress_waits_ten_minutes_gh_3069()
    {
        _ = _rig.Index.WithIndex(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New).WithEmbeddings(CodeIndexJobsRig.Demo, LayerState.Unavailable, code: "SND-CODEIDX-006");
        _rig.EmbedJob("demo", JobState.Succeeded, TimeSpan.FromMinutes(30), EmbedCodeIndexResult.Pending, embedded: 8_576);
        _rig.EmbedJob("demo", JobState.Succeeded, _minute, EmbedCodeIndexResult.Pending, embedded: 8_576);

        var held = await Run();
        _rig.Harness.Clock.Advance(TimeSpan.FromMinutes(10));
        var later = await Run();

        Assert.True(held.Value.Idle);
        Assert.False(later.Value.Idle);
        _ = Assert.Single(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_embeddings_layer_unavailable_for_a_pin_mismatch_starts_no_embed_gh_3069()
    {
        _ = _rig.Index.WithIndex(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New).WithEmbeddings(CodeIndexJobsRig.Demo, LayerState.Unavailable, code: "SND-CODEIDX-009");

        var result = await Run();

        Assert.True(result.Value.Idle);
        Assert.Empty(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_ok_embeddings_layer_is_quiet_with_no_start()
    {
        _ = _rig.Index.WithIndex(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New).WithEmbeddings(CodeIndexJobsRig.Demo, LayerState.Ok);

        var result = await Run();

        Assert.True(result.Value.Idle);
        Assert.Empty(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_with_no_index_yet_starts_nothing()
    {
        var result = await Run();

        Assert.True(result.Value.Idle);
        Assert.Empty(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_running_embed_of_the_repo_holds_the_start_and_another_repos_does_not()
    {
        _ = _rig.Index.WithIndex(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New).WithEmbeddings(CodeIndexJobsRig.Demo, LayerState.Pending)
            .WithIndex(CodeIndexJobsRig.Other, CodeIndexJobsRig.New).WithEmbeddings(CodeIndexJobsRig.Other, LayerState.Pending);
        _rig.EmbedJob("demo", JobState.Running, TimeSpan.Zero);

        _ = await Run();

        Assert.Equal(["other"], [.. _start.Requests.Select(static r => r.Repo)], StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_embed_backs_the_repo_off_and_names_it()
    {
        _ = _rig.Index.WithIndex(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New).WithEmbeddings(CodeIndexJobsRig.Demo, LayerState.Pending);
        _rig.EmbedJob("demo", JobState.Failed, _minute);

        var result = await Run();

        Assert.Empty(_start.Requests);
        Assert.Contains("demo", result.Value.Summary, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_busy_index_is_a_short_wait_and_not_a_failure()
    {
        _ = _rig.Index.WithIndex(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New).WithEmbeddings(CodeIndexJobsRig.Demo, LayerState.Pending);
        _rig.EmbedJob("demo", JobState.Succeeded, _minute, EmbedCodeIndexResult.Later);

        var held = await Run();
        _rig.Harness.Clock.Advance(_minute * 2);
        var later = await Run();

        Assert.True(held.Value.Idle);
        Assert.False(later.Value.Idle);
        Assert.Equal([new EmbedCodeIndexRequest("demo")], _start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_run_that_embedded_nothing_more_and_ended_pending_backs_off_ten_minutes()
    {
        _ = _rig.Index.WithIndex(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New).WithEmbeddings(CodeIndexJobsRig.Demo, LayerState.Pending);
        _rig.EmbedJob("demo", JobState.Succeeded, TimeSpan.FromMinutes(30), EmbedCodeIndexResult.Pending, embedded: 500);
        _rig.EmbedJob("demo", JobState.Succeeded, _minute, EmbedCodeIndexResult.Pending, embedded: 500);

        var held = await Run();
        _rig.Harness.Clock.Advance(TimeSpan.FromMinutes(10));
        var later = await Run();

        Assert.True(held.Value.Idle);
        Assert.False(later.Value.Idle);
        _ = Assert.Single(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_run_that_embedded_more_and_ended_pending_is_followed_at_once()
    {
        _ = _rig.Index.WithIndex(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New).WithEmbeddings(CodeIndexJobsRig.Demo, LayerState.Pending);
        _rig.EmbedJob("demo", JobState.Succeeded, TimeSpan.FromMinutes(30), EmbedCodeIndexResult.Pending, embedded: 500);
        _rig.EmbedJob("demo", JobState.Succeeded, _minute, EmbedCodeIndexResult.Pending, embedded: 1_000);

        _ = await Run();

        Assert.Equal([new EmbedCodeIndexRequest("demo")], _start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_build_job_of_the_repo_does_not_count_as_an_embed()
    {
        _ = _rig.Index.WithIndex(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New).WithEmbeddings(CodeIndexJobsRig.Demo, LayerState.Pending);
        _rig.Job("demo", JobState.Running, TimeSpan.Zero);

        _ = await Run();

        Assert.Equal([new EmbedCodeIndexRequest("demo")], _start.Requests);
    }

    private sealed class SilentLogger : ILogger<CodeIndexEmbedService>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }

    private sealed class Starter(SliceHarness harness) : IService<EmbedCodeIndexRequest, JobStartedResponse>
    {
        internal List<EmbedCodeIndexRequest> Requests { get; } = [];

        internal List<string> Actors { get; } = [];

        public ValueTask<Result<JobStartedResponse>> Handle(EmbedCodeIndexRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Actors.Add(harness.Callers.Current?.Actor ?? string.Empty);
            return ValueTask.FromResult<Result<JobStartedResponse>>(new JobStartedResponse(new CommandAccepted(harness.Ids.New<CommandId>(), 1), JobId.New(harness.Ids), new JobLinks("/j", "/c", "/e")));
        }
    }
}
