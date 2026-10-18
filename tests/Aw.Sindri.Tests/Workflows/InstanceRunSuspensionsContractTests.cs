using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The engine's answer to "is this launch's run suspended": read from the open instances' views, under the same contract the fake honours.</summary>
[Lane(TestLane.Unit)]
public sealed class InstanceRunSuspensionsContractTests : RunSuspensionsContract
{
    private static readonly InstanceAddress _address = InstanceAddress.From("T-1/workflow.step/1");
    private static readonly RecipeVersion _recipe = new(RecipeId.From("workflow.step"), 1, RecipeHash.From("sha256:" + new string('a', 64)));
    private readonly Views _views = new();

    protected override ValueTask<IRunSuspensions> Create() => ValueTask.FromResult<IRunSuspensions>(new InstanceRunSuspensions(_views, _views));

    protected override ValueTask Run(IRunSuspensions subject, LaunchId launch)
    {
        _views.State = "write";
        _views.Launch = launch.Value;
        return ValueTask.CompletedTask;
    }

    protected override ValueTask Suspend(IRunSuspensions subject, LaunchId launch)
    {
        _views.State = EngineStates.Parked;
        return ValueTask.CompletedTask;
    }

    protected override ValueTask Resume(IRunSuspensions subject, LaunchId launch)
    {
        _views.State = "write";
        return ValueTask.CompletedTask;
    }

    [Theory]
    [InlineData("parked")]
    [InlineData("escalated")]
    [InlineData("taken-over")]
    public async Task Every_engine_suspension_counts(string state)
    {
        _views.Launch = A.FirstLaunchId.Value;
        _views.State = state;

        var answer = await new InstanceRunSuspensions(_views, _views).IsSuspended(A.FirstLaunchId, TestContext.Current.CancellationToken);

        Expect.True(answer.IsSuccess && answer.Value);
    }

    [Fact]
    public async Task A_run_of_another_launch_is_not_this_ones()
    {
        _views.Launch = "LCH-" + new string('b', 32);
        _views.State = EngineStates.Parked;

        var answer = await new InstanceRunSuspensions(_views, _views).IsSuspended(A.FirstLaunchId, TestContext.Current.CancellationToken);

        Expect.True(answer.IsSuccess && !answer.Value);
    }

    [Fact]
    public async Task A_control_plane_that_cannot_list_is_an_error()
    {
        _views.Fails = true;

        var answer = await new InstanceRunSuspensions(_views, _views).IsSuspended(A.FirstLaunchId, TestContext.Current.CancellationToken);

        Expect.True(answer.IsError);
    }

    /// <summary>One open instance whose agent run (if it has a launch) is the stand-in for the engine's views.</summary>
    private sealed class Views : IWorkflowInstances, IInstanceReader
    {
        public string State { get; set; } = "write";

        public string? Launch { get; set; }

        public bool Fails { get; set; }

        public ValueTask<Result<IReadOnlyList<InstanceRow>>> List(string? query, bool ended, CancellationToken cancellationToken) => List(new InstanceFilter(), ended, cancellationToken);

        public ValueTask<Result<IReadOnlyList<InstanceRow>>> List(InstanceFilter filter, bool ended, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<IReadOnlyList<InstanceRow>>>(Fails
                ? Aw.Sindri.Core.SindriErrors.LaunchInterrupted.With("no control plane")
                : Launch is null || ended ? new List<InstanceRow>() : new List<InstanceRow> { new(_address.Value, State, NeedsYou: false) });

        public ValueTask<Result<InstanceView>> Get(InstanceAddress address, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<InstanceView>>(new InstanceView(address, _recipe, State, Detail: null, DateTimeOffset.UnixEpoch, string.Empty, [], Question: null, Task: null, Run: RunToken.For(1, 1), Children: [], Outcome: null)
            {
                Id = WorkflowInstanceId.Of(address),
                AgentRun = Launch is null ? null : new OpenRun(RunToken.For(1, 1), StepRuntime.Agent, Launch, Task: null),
            });
    }
}
