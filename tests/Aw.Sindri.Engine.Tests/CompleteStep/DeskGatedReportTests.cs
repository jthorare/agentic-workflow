using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Tests.Workflows;
using Aw.Sindri.Tests.Workflows.Spike;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Client;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.CompleteStep;

/// <summary>
/// A desk report the recipe's transition gates fails closed in P4 (W5): the instance refuses it and the mirrored ledger row
/// is 'workflow.refused', as the state loop's is, not a 'workflow.transition' (review run 2, architecture-strategist-r2-0).
/// </summary>
[Lane(TestLane.Integration)]
public sealed class DeskGatedReportTests(TimeSkippingServer server) : IClassFixture<TimeSkippingServer>, IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 120_000)]
    public async Task A_desk_report_on_a_gated_transition_writes_workflow_refused()
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        var gated = WorkflowsHarness.Shipped("sindri/workflow.step.yaml")
            .Replace("version: 5\n", "version: 6\n", StringComparison.Ordinal)
            .Replace("on: step.completed, when: { outcome: done }, produces", "on: step.completed, when: { outcome: done }, gates: [gate.test.check], produces", StringComparison.Ordinal);
        _harness.Recipe("workflow.step", gated);
        await DeskTimeoutTests.RegisterAttributes(env);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var running = worker.ExecuteAsync(stop.Token);
        var handle = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.StepStart(sindri, "T-9", ct));
        var desk = new DeskInstance(sindri, handle);
        try
        {
            var open = await desk.Until(static v => v.Task is not null);
            var output = new FactValue("fact:sha256:" + new string('e', 64), new string('e', 64), HeadSha: null);
            var report = new StepCompleted("human:cli", open.Run!.Value, OutcomeName.From("done"), new Dictionary<FactKey, FactValue> { [FactKey.From("fact:output")] = output }, Tokens: 0, StepRuntime.Desk, new string('f', 64));

            _ = await handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(report), new WorkflowUpdateOptions(new string('a', 32))).WaitAsync(TimeSpan.FromSeconds(30), ct);

            Assert.Equal(["workflow.started", "workflow.refused"], await desk.Events());
        }
        finally
        {
            await handle.TerminateAsync("test over");
            await stop.CancelAsync();
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        }
    }
}
