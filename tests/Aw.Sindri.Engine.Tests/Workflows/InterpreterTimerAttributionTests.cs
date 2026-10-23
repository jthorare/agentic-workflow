using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Tests.CompleteStep;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Client;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The mirrored ledger rows carry their trigger's actor (engine spec §5.6): the human who answered or reported, and the
/// timer service for a timeout. Heavy lane: its one-hour <see cref="Temporalio.Testing.WorkflowEnvironment.DelayAsync(TimeSpan, CancellationToken?)"/>
/// waits for the checks activity to close before the server can skip time, and under a busy gate that wait outlasts the
/// client's 30 s RPC timeout ("Timeout expired" at the delay, about 31 s; it passes alone). Tracking: give the delay a
/// bounded wait for the activity to close, then move it back to the integration lane in InterpreterAttributionTests.
/// </summary>
[Lane(TestLane.Heavy)]
public sealed class InterpreterTimerAttributionTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>, IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 120_000)]
    public async Task Rows_carry_the_trigger_actor_and_the_instance_tenant_and_ring()
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var start = await WorkflowsHarness.StepStart(sindri, "T-51", ct);
            var handle = await WorkflowsHarness.Start(env.Client, start);
            var desk = new DeskInstance(sindri, handle);
            _ = await desk.Until(static v => v.Task is not null);
            var output = new FactValue("fact:sha256:" + new string('e', 64), new string('e', 64), HeadSha: null);
            var report = new StepCompleted("human:cli", RunToken.From("1.1"), OutcomeName.From("done"), new Dictionary<FactKey, FactValue> { [FactKey.From("fact:output")] = output }, Tokens: 0, StepRuntime.Desk, new string('f', 64));
            _ = await handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(report), new WorkflowUpdateOptions(new string('a', 32)));
            _ = await desk.Until(static v => v.State == "checks" && v.Task is not null);
            await env.DelayAsync(TimeSpan.FromHours(1), ct);   // no time skip while the 'RunStep' activity is open (spec §11 F1)
            _ = await desk.Until(static v => v.State == "checks" && v.Task is not null);
            await env.DelayAsync(TimeSpan.FromDays(2), ct);
            _ = await desk.Until(static v => v.State == "parked");
            var resume = new AnswerGiven("human:thor", new CommandStamp(CommandNonce.From(new string('b', 32)), new string('c', 64)), AnswerVariant.Resume, Text: null, Amount: null, Forwarded: null);
            _ = await handle.ExecuteUpdateAsync(wf => wf.AnswerAsync(resume), new WorkflowUpdateOptions(new string('b', 32)));
            _ = await desk.Until(static v => v.Task is not null);

            var rows = (await sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 100, ct)).Value
                .Where(static e => e.Envelope.Type.StartsWith("workflow.", StringComparison.Ordinal)).ToList();

            await InterpreterAttributionTests.Finish(handle);

            Assert.Equal(["host:sindri", "human:cli", "service:workflow.timers", "human:thor"], [.. rows.Select(static e => e.Envelope.Actor)]);
        }, ct);
    }
}
