using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Interpolation;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Features.RunWorkflow;
using Aw.Sindri.Tests.Graph.Blocks;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.RunWorkflow;

/// <summary>'sindri wf run': a manual trigger of the active revision, refusing what can't run and passing the starter's answer through.</summary>
[Lane(TestLane.Unit)]
public sealed class RunWorkflowServiceTests
{
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("morning-check");

    private readonly FakeTimeProvider _clock = new();
    private readonly InMemoryWorkflowStore _workflows;
    private readonly InMemoryWorkflowRunStore _runs;
    private readonly FakeStarter _starter;
    private readonly FakeLaunches _launches = new();
    private readonly SequentialIdGenerator _ids = new();
    private readonly AsyncLocalCallerContextAccessor _callers;
    private readonly RunWorkflowService _service;

    public RunWorkflowServiceTests()
    {
        _workflows = new InMemoryWorkflowStore(_clock);
        _runs = new InMemoryWorkflowRunStore(_clock);
        _starter = new FakeStarter(_runs, _clock);
        _callers = new AsyncLocalCallerContextAccessor(_ids);
        _service = new RunWorkflowService(_workflows, _runs, _starter, _launches, _callers, _clock);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_manual_run_of_the_active_revision_starts_with_its_payload()
    {
        await Create(concurrency: "queue", activate: true);

        var result = await _service.Handle(new RunWorkflowRequest(_slug, "{\"repo\":\"api\"}", "session:s1"), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal("started", result.Value.Status);
        var request = Assert.Single(_starter.Requests);
        Assert.Equal(_id, request.Workflow);
        Assert.Equal(1, request.Revision);
        Assert.Equal(GraphConcurrency.Queue, request.Concurrency);
        var trigger = JsonNode.Parse(request.Trigger)!;
        Assert.Equal("manual", trigger["kind"]!.GetValue<string>());
        Assert.Equal("api", trigger["payload"]!["repo"]!.GetValue<string>());
        Assert.Equal("session:s1", trigger["by"]!.GetValue<string>());
        Assert.Equal(WorkflowRunId.Of(_slug, 1, request.Occurrence), result.Value.Run);
    }

    [Fact]
    public async Task A_hand_start_of_a_schedule_node_gives_the_start_time_local_date_and_weekday_in_its_zone()
    {
        // Tokyo is UTC+9: Thu 12 Nov 22:00 UTC is Fri 13 Nov 07:00 there.
        _clock.SetUtcNow(DateTimeOffset.Parse("2026-11-12T22:00:00Z", CultureInfo.InvariantCulture));
        await CreateSchedule();

        var result = await _service.Handle(new RunWorkflowRequest(_slug, Payload: null, "owner"), Token);

        Assert.True(result.IsSuccess);
        var trigger = JsonNode.Parse(Assert.Single(_starter.Requests).Trigger)!;
        Assert.Equal("2026-11-12T22:00:00.0000000Z", trigger["scheduledFor"]?.GetValue<string>());
        Assert.Equal("2026-11-13", trigger["localDate"]?.GetValue<string>());
        Assert.Equal("Friday", trigger["weekday"]?.GetValue<string>());
    }

    [Fact]
    public async Task A_prompt_reading_the_weekday_of_a_hand_started_schedule_node_gets_the_day_name()
    {
        _clock.SetUtcNow(DateTimeOffset.Parse("2026-11-12T22:00:00Z", CultureInfo.InvariantCulture));
        await CreateSchedule();
        _ = await _service.Handle(new RunWorkflowRequest(_slug, Payload: null, "owner"), Token);
        var trigger = JsonNode.Parse(Assert.Single(_starter.Requests).Trigger)!.AsObject();
        var values = trigger.ToDictionary(static p => "trigger." + p.Key, static p => p.Value, StringComparer.Ordinal);
        var daily = await new ScheduleBlock().Execute(BlockFakes.Context(new JsonObject { ["cron"] = "0 7 * * 1-5" }, values), Token);

        var prompt = Template.Render("Today is {{daily.weekday}} ({{daily.localDate}})", reference => daily.Value.Outputs.GetValueOrDefault(reference.Replace("daily.", string.Empty, StringComparison.Ordinal)));

        Assert.Equal("Today is Friday (2026-11-13)", prompt?.GetValue<string>());
    }

    [Fact]
    public async Task A_hand_start_on_a_Wednesday_in_the_node_zone_gives_Wednesday_while_UTC_is_still_Tuesday()
    {
        // why: a Run now on a non-Friday must not read as Friday (ideas on #3533: an empty date let the model guess the day)
        _clock.SetUtcNow(DateTimeOffset.Parse("2026-11-10T20:00:00Z", CultureInfo.InvariantCulture));
        await CreateSchedule();

        _ = await _service.Handle(new RunWorkflowRequest(_slug, Payload: null, "owner"), Token);

        var trigger = JsonNode.Parse(Assert.Single(_starter.Requests).Trigger)!.AsObject();
        Assert.Equal("Wednesday", trigger["weekday"]?.GetValue<string>());
        Assert.Equal("2026-11-11", trigger["localDate"]?.GetValue<string>());
    }

    [Fact]
    public async Task A_session_launched_by_a_run_of_the_same_workflow_is_refused_SND_GRAPH_109()
    {
        await Create(concurrency: null, activate: true);
        var launch = LaunchId.From("LCH-" + new string('0', 31) + "1");
        var parent = WorkflowRunId.Of(_slug, 3, 12);
        _launches.Add(launch, parent);
        using var caller = _callers.Enter(new CallerContext(CallerClass.Session, Terminal: false, "session:s1", _ids.New<CommandId>(), _ids.New<CorrelationId>(), Repo: null, launch));

        var result = await _service.Handle(new RunWorkflowRequest(_slug, Payload: null, "session:s1"), Token);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.GraphRunLoop.Code, result.Error.Code);
        Assert.Contains(parent.Value, result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_starter.Requests);
    }

    [Fact]
    public async Task A_session_launched_by_a_run_of_another_workflow_may_run_it()
    {
        await Create(concurrency: null, activate: true);
        var launch = LaunchId.From("LCH-" + new string('0', 31) + "1");
        _launches.Add(launch, WorkflowRunId.Of(WorkflowSlug.From("morning-checker"), 1, 1));
        using var caller = _callers.Enter(new CallerContext(CallerClass.Session, Terminal: false, "session:s1", _ids.New<CommandId>(), _ids.New<CorrelationId>(), Repo: null, launch));

        var result = await _service.Handle(new RunWorkflowRequest(_slug, Payload: null, "session:s1"), Token);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task A_caller_with_no_launch_or_no_caller_may_run_it()
    {
        await Create(concurrency: "queue", activate: true);

        Assert.True((await _service.Handle(new RunWorkflowRequest(_slug, Payload: null, "owner"), Token)).IsSuccess);
        using var caller = _callers.Enter(new CallerContext(CallerClass.Human, Terminal: true, "human:cli", _ids.New<CommandId>(), _ids.New<CorrelationId>(), Repo: null));
        Assert.True((await _service.Handle(new RunWorkflowRequest(_slug, Payload: null, "human:cli"), Token)).IsSuccess);
    }

    [Fact]
    public async Task Two_manual_runs_get_two_run_ids()
    {
        await Create(concurrency: null, activate: true);

        var first = await _service.Handle(new RunWorkflowRequest(_slug, Payload: null, "owner"), Token);
        var second = await _service.Handle(new RunWorkflowRequest(_slug, Payload: null, "owner"), Token);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.NotEqual(first.Value.Run, second.Value.Run);
    }

    [Fact]
    public async Task An_unknown_workflow_is_refused()
    {
        var result = await _service.Handle(new RunWorkflowRequest(_slug, Payload: null, "owner"), Token);

        Assert.Equal(SindriErrors.GraphNotFound.Code, result.Error.Code);
        Assert.Empty(_starter.Requests);
    }

    [Fact]
    public async Task A_workflow_with_no_active_revision_is_refused()
    {
        await Create(concurrency: null, activate: false);

        var result = await _service.Handle(new RunWorkflowRequest(_slug, Payload: null, "owner"), Token);

        Assert.Equal(SindriErrors.GraphNotActive.Code, result.Error.Code);
        Assert.Empty(_starter.Requests);
    }

    [Fact]
    public async Task A_skip_by_one_per_workflow_passes_through_with_its_reason()
    {
        await Create(concurrency: "one-per-workflow", activate: true);
        _starter.Answer = GraphStartKind.Skipped;

        var result = await _service.Handle(new RunWorkflowRequest(_slug, Payload: null, "owner"), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal("skipped", result.Value.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.Reason));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1, 2]")]
    [InlineData("\"text\"")]
    public async Task A_payload_that_is_not_a_json_object_is_refused(string payload)
    {
        await Create(concurrency: null, activate: true);

        var result = await _service.Handle(new RunWorkflowRequest(_slug, payload, "owner"), Token);

        Assert.Equal(SindriErrors.GraphBadPayload.Code, result.Error.Code);
        Assert.Empty(_starter.Requests);
    }

    [Fact]
    public async Task Setup_service_is_refused_for_another_workflow_slug()
    {
        await Create(concurrency: null, activate: true);
        using var caller = _callers.EnterService("setup");

        var result = await _service.Handle(new RunWorkflowRequest(_slug, Payload: null, "service:setup"), Token);

        Assert.Equal(SindriErrors.GraphSetupRunRefused.Code, result.Error.Code);
        Assert.Empty(_starter.Requests);
    }

    [Fact]
    public async Task Setup_service_is_refused_a_second_run_of_hello_sindri()
    {
        await Create(concurrency: "unlimited", activate: true, WorkflowSlug.From("hello-sindri"), "service:setup");
        using var caller = _callers.EnterService("setup");
        var first = await _service.Handle(new RunWorkflowRequest(WorkflowSlug.From("hello-sindri"), Payload: null, "service:setup"), Token);

        var second = await _service.Handle(new RunWorkflowRequest(WorkflowSlug.From("hello-sindri"), Payload: null, "service:setup"), Token);

        Assert.True(first.IsSuccess);
        Assert.Equal(SindriErrors.GraphSetupRunRefused.Code, second.Error.Code);
        _ = Assert.Single(_starter.Requests);
    }

    [Fact]
    public async Task Another_caller_may_run_hello_sindri_again()
    {
        await Create(concurrency: "unlimited", activate: true, WorkflowSlug.From("hello-sindri"), "service:setup");
        using (var caller = _callers.EnterService("setup"))
        {
            _ = await _service.Handle(new RunWorkflowRequest(WorkflowSlug.From("hello-sindri"), Payload: null, "service:setup"), Token);
        }

        var again = await _service.Handle(new RunWorkflowRequest(WorkflowSlug.From("hello-sindri"), Payload: null, "owner"), Token);

        Assert.True(again.IsSuccess);
    }

    private async Task Create(string? concurrency, bool activate, WorkflowSlug? slug = null, string createdBy = "owner")
    {
        var spec = new GraphSpec(Version: 1, Name: "Morning check", Description: null, Concurrency: concurrency, Resources: null,
            Nodes: [new GraphNode(NodeId.From("start"), "manual", Title: null, [])], Edges: []);
        _ = (await _workflows.Create(_id, slug ?? _slug, spec, createdBy, Token)).Value;
        if (activate)
        {
            _ = (await _workflows.Activate(_id, 1, Token)).Value;
        }
    }

    private async Task CreateSchedule()
    {
        var spec = new GraphSpec(Version: 1, Name: "Morning check", Description: null, Concurrency: null, Resources: null,
            Nodes: [new GraphNode(NodeId.From("daily"), "schedule", Title: null, new JsonObject { ["cron"] = "0 7 * * 1-5", ["timezone"] = "Asia/Tokyo" })], Edges: []);
        _ = (await _workflows.Create(_id, _slug, spec, "owner", Token)).Value;
        _ = (await _workflows.Activate(_id, 1, Token)).Value;
    }

    /// <summary>Maps a launch to the run that launched it.</summary>
    private sealed class FakeLaunches : IGraphLaunchRuns
    {
        private readonly Dictionary<LaunchId, WorkflowRunId> _runs = [];

        public void Add(LaunchId launch, WorkflowRunId run) => _runs[launch] = run;

        public ValueTask<Result<WorkflowRunId?>> RunOf(LaunchId launch, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<WorkflowRunId?>>(_runs.TryGetValue(launch, out var run) ? (WorkflowRunId?)run : null);
    }

    /// <summary>Like the real starter, it admits the run into the store, so the next manual run sees it; a set <see cref="Answer"/> stands in for a queue or skip answer and admits nothing.</summary>
    private sealed class FakeStarter(InMemoryWorkflowRunStore runs, TimeProvider clock) : IGraphRunStarter
    {
        public List<GraphStartRequest> Requests { get; } = [];

        public GraphStartKind? Answer { get; set; }

        public async ValueTask<Result<GraphStart>> Start(GraphStartRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var id = WorkflowRunId.Of(request.Slug, request.Revision, request.Occurrence);
            if (Answer is { } fixedAnswer)
            {
                return new GraphStart(fixedAnswer, id);
            }

            var run = new WorkflowRun(id, request.Workflow, request.Revision, request.Trigger, WorkflowRunStatus.Running, Outputs: null, Error: null, ItemId: null, clock.GetUtcNow(), EndedAt: null);
            var admitted = await runs.Admit(run, request.Concurrency, cancellationToken);
            return admitted.IsError ? admitted.Error : new GraphStart(admitted.Value, id);
        }
    }
}
