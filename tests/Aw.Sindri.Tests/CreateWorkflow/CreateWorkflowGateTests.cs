using Aw.Kernel.Services;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Validation;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.CreateWorkflow;
using Aw.Sindri.Host.Catalog;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Commands.Chain;
using Aw.Sindri.Tests.WorkflowWrites;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.CreateWorkflow;

/// <summary>
/// The create's draft check compiles every code node (cold Roslyn, 4.3 s on a fresh start, #3571). It must not run inside the command gate,
/// or the next command waits behind it and meets SND-API-009 at 5 s.
/// </summary>
public sealed class CreateWorkflowGateTests
{
    private static CallerContext Human => A.Caller().AtTerminal().Client(ClientLabel.Cli).Build();

    [Fact, Trait("Lane", "unit")]
    public async Task A_command_waits_less_than_half_a_second_behind_a_create_that_compiles_slowly()
    {
        var token = TestContext.Current.CancellationToken;
        using var inner = new WorkflowWriteRig(realBlocks: true);
        var slow = new SlowChecker(inner.Checker);
        await using var chain = new ChainHarness(
            register: (services, h) => services
                .AddSingleton<IWorkflowStore>(new InMemoryWorkflowStore(h.Clock))
                .AddSingleton<IWorkflowChecker>(slow)
                .AddSingleton(new WorkflowChangeLog(h.Store, h.Held, h.Events, h.Callers))
                .AddScoped<IValidator<CreateWorkflowRequest>, AcceptAllValidator<CreateWorkflowRequest>>()
                .AddCommand<CreateWorkflowRequest, CreateWorkflowResponse, CreateWorkflowService>(),
            rows: WorkflowRows.All);
        _ = await chain.H.HoldLease();
        var create = chain.Run<CreateWorkflowRequest, CreateWorkflowResponse>(new CreateWorkflowRequest(WorkflowWriteRig.Slug, "manual-notify", Spec: null, "owner"), Human);
        await slow.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System, token);   // the create is inside its compile

        try
        {
            var second = chain.Start(new FixtureCommand("accept"), Human);
            var first = await Task.WhenAny(second, Task.Delay(TimeSpan.FromMilliseconds(500), TimeProvider.System, token));

            Assert.Same(second, first);   // it got through while the compile was still running
            Assert.True((await second).IsSuccess);
        }
        finally
        {
            slow.Release.SetResult();
        }

        Assert.True((await create.WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System, token)).IsSuccess);
    }

    private sealed class SlowChecker(IWorkflowChecker real) : IWorkflowChecker
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<GraphValidator> Validator(CancellationToken cancellationToken)
        {
            _ = Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return await real.Validator(cancellationToken).ConfigureAwait(false);
        }
    }
}
