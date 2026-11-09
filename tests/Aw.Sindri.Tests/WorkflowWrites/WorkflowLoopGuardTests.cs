using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Host.Mcp;

namespace Aw.Sindri.Tests.WorkflowWrites;

/// <summary>'workflow_run' loop guard: a session a run of a workflow launched can't start that workflow again.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowLoopGuardTests
{
    private static readonly LaunchId _launch = LaunchId.From("LCH-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("drift-check");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_session_launched_by_a_run_cannot_run_its_own_workflow()
    {
        var run = WorkflowRunId.Of(_slug, 3, 12);

        var refusal = await WorkflowLoopGuard.Check(new Launches(run), _launch, _slug, Token);

        Assert.Equal(SindriErrors.GraphRunLoop.Code, refusal!.Code);
        Assert.Contains(run.Value, refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_session_launched_by_a_run_of_another_workflow_may_run_this_one()
    {
        var other = WorkflowRunId.Of(WorkflowSlug.From("drift-checker"), 1, 1);

        Assert.Null(await WorkflowLoopGuard.Check(new Launches(other), _launch, _slug, Token));
    }

    [Fact]
    public async Task A_person_or_a_session_no_run_launched_may_run_it()
    {
        Assert.Null(await WorkflowLoopGuard.Check(new Launches(run: null), launch: null, _slug, Token));
        Assert.Null(await WorkflowLoopGuard.Check(new Launches(run: null), _launch, _slug, Token));
    }

    private sealed class Launches(WorkflowRunId? run) : IGraphLaunchRuns
    {
        public ValueTask<Result<WorkflowRunId?>> RunOf(LaunchId launch, CancellationToken cancellationToken) => ValueTask.FromResult<Result<WorkflowRunId?>>(run);
    }
}
