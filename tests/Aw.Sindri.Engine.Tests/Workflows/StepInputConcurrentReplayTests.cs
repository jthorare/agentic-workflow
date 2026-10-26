using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.Workflows.Inputs;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The history of a Step whose inputs filled together ('step-inputs-concurrent-v1') replays on this build, and a failing fill among them is still
/// the Step's failure.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class StepInputConcurrentReplayTests : IDisposable
{
    private readonly WorkflowsHarness _harness = new();
    private readonly InputProbe _probe = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task The_history_of_inputs_filled_together_replays()
    {
        await using var env = await WorkflowsHarness.StartDevServer(ct: Token);
        _harness.Recipe("workflow.inputs", Recipe, AddProbes);
        await using var sindri = _harness.Sindri(AddProbes, name => name == "AW_SINDRI_TEMPORAL_NAMESPACE" ? env.Client.Options.Namespace : null);
        await WorkflowsHarness.HoldLease(sindri, Token);
        _ = await WorkflowsHarness.AsServe(sindri, env, Token);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.RecipeStart(sindri, "workflow.inputs", "T-1101r", Token));

            var view = await WorkflowsHarness.Until(handle, static v => v.Task is not null);

            Assert.Equal("checks", view.State);
            var replayed = await new WorkflowReplayer(new WorkflowReplayerOptions().AddWorkflow<RecipeWorkflow>()).ReplayWorkflowAsync(await handle.FetchHistoryAsync(), throwOnReplayFailure: false, Token);
            Assert.Null(replayed.ReplayFailure);
            await handle.TerminateAsync("test over");
        }, Token);
    }

    private void AddProbes(IServiceCollection services) => services.AddSingleton(_probe).AddStepInput<ProbeInput>().AddStepInput<SecondProbeInput>();

    private static string Recipe => string.Join('\n',
    [
        "schema: 1",
        "id: workflow.inputs",
        "version: 1",
        "inputs: [{ key: fact:item, type: item, kind: item }]",
        "initial: checks",
        "states:",
        "  checks: { kind: active, step: { use: step.checks, inputs: [" + ProbeInput.Id + ", " + SecondProbeInput.Id + "] }, timeout: { after: 30m, then: park }, next: \"Run the checks.\" }",
        "  done:   { kind: terminal, outcome: resolved }",
        "  broken: { kind: terminal, outcome: failed }",
        "transitions:",
        "  - { from: checks, to: done, on: step.completed, when: { outcome: passed } }",
        "  - { from: checks, to: broken, on: step.failed }",
    ]);
}
