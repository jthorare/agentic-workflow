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
/// A mirrored row carries the event's full payload (engine spec rev 8.1 §5.6): the trigger's id, actor and digest, the gates
/// that answered and what followed a refusal, never a fact's content (review run 2, architecture-strategist-r2-1 and
/// data-integrity-guardian-r2-2).
/// </summary>
[Lane(TestLane.Integration)]
public sealed class DeskEventDetailTests(TimeSkippingServer server) : IClassFixture<TimeSkippingServer>, IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 120_000)]
    public async Task A_refused_row_names_its_trigger_its_gate_and_what_followed()
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

            var row = Assert.Single(await desk.Payloads("workflow.refused"));
            var detail = row.GetProperty("detail");

            Assert.Equal("human:cli", detail.GetProperty("triggerActor").GetString());
            Assert.EndsWith(new string('f', 64), detail.GetProperty("triggerId").GetString(), StringComparison.Ordinal);
            Assert.Equal(new string('f', 64), detail.GetProperty("reportSha256").GetString());
            Assert.Equal("gate.test.check", Assert.Single(detail.GetProperty("gates").EnumerateArray()).GetProperty("gate").GetString());
            Assert.Equal("rerun", detail.GetProperty("refusal").GetProperty("follow").ToString());
            Assert.DoesNotContain(new string('e', 64), row.GetRawText(), StringComparison.Ordinal);   // the output fact's digest is not in a refused row; its content never is
        }
        finally
        {
            await handle.TerminateAsync("test over");
            await stop.CancelAsync();
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        }
    }

    [Fact(Timeout = 120_000)]
    public async Task An_applied_report_row_names_its_frame_and_its_output_digest_and_the_started_row_names_its_start()
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        await DeskTimeoutTests.RegisterAttributes(env);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var running = worker.ExecuteAsync(stop.Token);
        var handle = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.StepStart(sindri, "T-10", ct));
        var desk = new DeskInstance(sindri, handle);
        try
        {
            var open = await desk.Until(static v => v.Task is not null);
            var output = new FactValue("fact:sha256:" + new string('e', 64), new string('e', 64), HeadSha: null);
            var report = new StepCompleted("human:cli", open.Run!.Value, OutcomeName.From("done"), new Dictionary<FactKey, FactValue> { [FactKey.From("fact:output")] = output }, Tokens: 0, StepRuntime.Desk, new string('f', 64));
            _ = await handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(report), new WorkflowUpdateOptions(new string('a', 32))).WaitAsync(TimeSpan.FromSeconds(30), ct);

            var started = Assert.Single(await desk.Payloads("workflow.started")).GetProperty("detail");
            var applied = Assert.Single(await desk.Payloads("workflow.transition")).GetProperty("detail");

            Assert.Equal("human:desk", started.GetProperty("triggerActor").GetString());
            Assert.Equal($"wf:{WorkflowInstanceId.Of(InstanceAddress.From("T-10/step/1")).Value}:start", started.GetProperty("triggerId").GetString());
            Assert.Equal("checks", applied.GetProperty("delta").GetProperty("frame").GetProperty("state").GetString());
            Assert.Equal(new string('e', 64), applied.GetProperty("delta").GetProperty("facts").GetProperty("fact:output").GetString());
        }
        finally
        {
            await handle.TerminateAsync("test over");
            await stop.CancelAsync();
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        }
    }
}
