using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.Workflows.Inputs;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Client;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The inputs of one Step fill together, so each fill must keep its own DI scope (#1101): the context engine is safe to share only that way.</summary>
[Lane(TestLane.Integration)]
public sealed class StepInputScopeTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>, IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task Each_input_fills_in_its_own_DI_scope()
    {
        var log = new ScopeLog();
        var ct = TestContext.Current.CancellationToken;
        _harness.Recipe("workflow.inputs", Recipe, Add);
        await using var sindri = _harness.Sindri(Add);
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(server.Environment.Client, sindri.GetRequiredService<TemporalWorkerOptions>());
        await worker.ExecuteAsync(async () =>
        {
            var handle = await WorkflowsHarness.Start(server.Environment.Client, await WorkflowsHarness.RecipeStart(sindri, "workflow.inputs", "T-1101d", ct));
            await WaitForRunStep(handle);

            Assert.Equal(3, log.Scopes.Distinct().Count());
            await handle.TerminateAsync("test over");
        }, ct);

        void Add(IServiceCollection services) => services.AddSingleton(log).AddScoped<ScopeMarker>().AddStepInput<ScopeProbeInput>().AddStepInput<ScopeProbeTwoInput>().AddStepInput<ScopeProbeThreeInput>();
    }

    private static async Task WaitForRunStep(WorkflowHandle<RecipeWorkflow, RecipeResult> handle)
    {
        var deadline = RealTime.NowDateTime + TimeSpan.FromSeconds(30);
        while (RealTime.NowDateTime < deadline)
        {
            if ((await handle.FetchHistoryAsync()).Events.Any(static e => e.ActivityTaskScheduledEventAttributes?.ActivityType.Name == "RunStep"))
            {
                return;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        Assert.Fail("RunStep was never scheduled");
    }

    private static string Recipe => string.Join('\n',
    [
        "schema: 1",
        "id: workflow.inputs",
        "version: 1",
        "inputs: [{ key: fact:item, type: item, kind: item }]",
        "initial: checks",
        "states:",
        "  checks: { kind: active, step: { use: step.checks, inputs: [" + ScopeProbeInput.Id + ", " + ScopeProbeTwoInput.Id + ", " + ScopeProbeThreeInput.Id + "] }, timeout: { after: 30m, then: park }, next: \"Run the checks.\" }",
        "  done:   { kind: terminal, outcome: resolved }",
        "  broken: { kind: terminal, outcome: failed }",
        "transitions:",
        "  - { from: checks, to: done, on: step.completed, when: { outcome: passed } }",
        "  - { from: checks, to: broken, on: step.failed }",
    ]);
}
