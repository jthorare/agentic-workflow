using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Features.RunWorkflow;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Setup;

/// <summary>#3496 item 2: the setup grant's once-only check reads every run of hello-sindri, even when the run store returns fewer rows than asked for.</summary>
[Lane(TestLane.Unit)]
public sealed class SetupRefusalPagingTests
{
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _hello = WorkflowSlug.From("hello-sindri");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_setup_run_older_than_the_first_clamped_page_still_refuses_a_second_run()
    {
        var clock = new FakeTimeProvider();
        var workflows = new InMemoryWorkflowStore(clock);
        var runs = new InMemoryWorkflowRunStore(clock) { RecentPageClamp = 50 };   // the store returns at most 50 rows, however many are asked for
        var callers = new AsyncLocalCallerContextAccessor(new SequentialIdGenerator());
        var service = new RunWorkflowService(workflows, runs, new NeverStarts(), new NoLaunches(), callers, clock);
        var spec = new GraphSpec(Version: 1, Name: "Hello", Description: null, Concurrency: "unlimited", Resources: null,
            Nodes: [new GraphNode(NodeId.From("start"), "manual", Title: null, [])], Edges: []);
        _ = (await workflows.Create(_id, _hello, spec, "service:setup", Token)).Value;
        _ = (await workflows.Activate(_id, 1, Token)).Value;
        await Admit(runs, clock, occurrence: 1, by: "service:setup");   // the oldest run: the setup service's one
        for (var occurrence = 2; occurrence <= 81; occurrence++)
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            await Admit(runs, clock, occurrence, by: "owner");   // 80 newer runs by the owner: the setup run is row 81, on the second page of 50 and past the first
        }

        using var caller = callers.EnterService("setup");
        var again = await service.Handle(new RunWorkflowRequest(_hello, Payload: null, "service:setup"), Token);

        Assert.Equal(SindriErrors.GraphSetupRunRefused.Code, again.Error.Code);
    }

    private static async Task Admit(InMemoryWorkflowRunStore runs, TimeProvider clock, int occurrence, string by)
    {
        var run = new WorkflowRun(WorkflowRunId.Of(_hello, 1, occurrence), _id, 1, $"{{\"kind\":\"manual\",\"by\":\"{by}\"}}", WorkflowRunStatus.Succeeded, Outputs: null, Error: null, ItemId: null, clock.GetUtcNow(), EndedAt: clock.GetUtcNow());
        _ = (await runs.Admit(run, GraphConcurrency.Unlimited, Token)).Value;
    }

    private sealed class NeverStarts : IGraphRunStarter
    {
        public ValueTask<Result<GraphStart>> Start(GraphStartRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class NoLaunches : IGraphLaunchRuns
    {
        public ValueTask<Result<WorkflowRunId?>> RunOf(LaunchId launch, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<WorkflowRunId?>>((WorkflowRunId?)null);
    }
}
