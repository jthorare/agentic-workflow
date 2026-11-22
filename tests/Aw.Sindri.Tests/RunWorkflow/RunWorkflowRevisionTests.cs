using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Features.RunWorkflow;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;
using static Aw.Sindri.Tests.WorkflowWrites.WorkflowWriteRig;

namespace Aw.Sindri.Tests.RunWorkflow;

/// <summary>
/// 'sindri wf run --revision' (#3691): one real run of a saved revision that is not live. It must be a revision the workflow has and pass the activation checks; the live revision stays,
/// and a workflow with no live revision is refused like a plain run.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class RunWorkflowRevisionTests
{
    private const string NoTrigger = "{\"version\":1,\"name\":\"No trigger\",\"nodes\":[{\"id\":\"tell\",\"type\":\"notify\",\"config\":{\"title\":\"x\"}}],\"edges\":[]}";

    private readonly FakeTimeProvider _clock = new();
    private readonly InMemoryWorkflowStore _workflows;
    private readonly Starter _starter = new();
    private readonly SequentialIdGenerator _ids = new();
    private readonly AsyncLocalCallerContextAccessor _callers;
    private readonly RunWorkflowService _service;

    public RunWorkflowRevisionTests()
    {
        _workflows = new InMemoryWorkflowStore(_clock);
        _callers = new AsyncLocalCallerContextAccessor(_ids);
        _service = new RunWorkflowService(_workflows, new InMemoryWorkflowRunStore(_clock), _starter, new NoLaunches(), _callers, _clock);
    }

    [Fact]
    public async Task A_saved_revision_runs_once_and_the_live_revision_stays()
    {
        await Live(SampleGraph());

        var result = await _service.Handle(new RunWorkflowRequest(Slug, Payload: null, "owner", Revision: 2), Token);

        Assert.True(result.IsSuccess);
        var started = Assert.Single(_starter.Requests);
        Assert.Equal(2, started.Revision);
        Assert.Equal(WorkflowRunId.Of(Slug, 2, started.Occurrence), result.Value.Run);
        Assert.Equal(1, (await _workflows.Get(Id, Token)).Value.ActiveRevision);
    }

    [Fact]
    public async Task No_revision_still_runs_the_live_one()
    {
        await Live(SampleGraph());

        _ = await _service.Handle(new RunWorkflowRequest(Slug, Payload: null, "owner"), Token);

        Assert.Equal(1, Assert.Single(_starter.Requests).Revision);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task A_revision_the_workflow_does_not_have_is_SND_GRAPH_008(int revision)
    {
        await Live(SampleGraph());

        var result = await _service.Handle(new RunWorkflowRequest(Slug, Payload: null, "owner", revision), Token);

        Assert.Equal(SindriErrors.GraphNotFound.Code, result.Error.Code);
        Assert.Empty(_starter.Requests);
    }

    [Fact]
    public async Task A_revision_that_fails_the_activation_checks_is_refused_like_activate()
    {
        await Live(GraphJson.Parse(NoTrigger).Value);

        var result = await _service.Handle(new RunWorkflowRequest(Slug, Payload: null, "owner", Revision: 2), Token);

        Assert.Equal(SindriErrors.GraphFindings.Code, result.Error.Code);
        Assert.Empty(_starter.Requests);
    }

    [Fact]
    public async Task A_workflow_that_was_never_live_is_refused_like_a_plain_run()
    {
        _ = (await _workflows.Create(Id, Slug, SampleGraph(), "owner", Token)).Value;

        var result = await _service.Handle(new RunWorkflowRequest(Slug, Payload: null, "owner", Revision: 1), Token);

        Assert.Equal(SindriErrors.GraphNotActive.Code, result.Error.Code);
        Assert.Empty(_starter.Requests);
    }

    [Fact]
    public async Task A_paused_workflow_is_refused_like_a_plain_run()
    {
        await Live(SampleGraph());
        _ = (await _workflows.Pause(Id, Token)).Value;

        var result = await _service.Handle(new RunWorkflowRequest(Slug, Payload: null, "owner", Revision: 2), Token);

        Assert.Equal(SindriErrors.GraphNotActive.Code, result.Error.Code);
        Assert.Empty(_starter.Requests);
    }

    [Fact]
    public async Task A_session_may_run_a_saved_revision_as_it_may_run_the_live_one()
    {
        await Live(SampleGraph());
        using var caller = _callers.Enter(new CallerContext(CallerClass.Session, Terminal: false, "session:s1", _ids.New<CommandId>(), _ids.New<CorrelationId>(), Repo: null));

        var result = await _service.Handle(new RunWorkflowRequest(Slug, Payload: null, "session:s1", Revision: 2), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, Assert.Single(_starter.Requests).Revision);
    }

    // Revision 1 is live; revision 2 (the given spec) is saved and not live.
    private async Task Live(GraphSpec second)
    {
        _ = (await _workflows.Create(Id, Slug, SampleGraph(), "owner", Token)).Value;
        _ = (await _workflows.Activate(Id, 1, Token)).Value;
        _ = (await _workflows.Save(Id, 1, second, "owner", Token)).Value;
    }

    private sealed class Starter : IGraphRunStarter
    {
        public List<GraphStartRequest> Requests { get; } = [];

        public ValueTask<Result<GraphStart>> Start(GraphStartRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return ValueTask.FromResult<Result<GraphStart>>(new GraphStart(GraphStartKind.Started, WorkflowRunId.Of(request.Slug, request.Revision, request.Occurrence)));
        }
    }

    private sealed class NoLaunches : IGraphLaunchRuns
    {
        public ValueTask<Result<WorkflowRunId?>> RunOf(LaunchId launch, CancellationToken cancellationToken) => ValueTask.FromResult<Result<WorkflowRunId?>>((WorkflowRunId?)null);
    }
}
