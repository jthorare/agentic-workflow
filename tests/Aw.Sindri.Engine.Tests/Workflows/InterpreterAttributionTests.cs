using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Client;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// What the mirrored ledger rows say about who did it and for whom (engine spec §5.6; review run 1, P4 interpreter): a
/// row carries its trigger's actor (the human who answered or reported, the timer service for a timeout) and the
/// instance's tenant and ring, not the worker process's scope; and the instance's own id is the one its address derives.
/// The trigger-actor rows are pinned in <see cref="InterpreterTimerAttributionTests"/>, on a server of its own.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class InterpreterAttributionTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>, IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 120_000)]
    public async Task Rows_carry_the_instance_tenant_and_ring()
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var start = await WorkflowsHarness.StepStart(sindri, "T-55", ct) with { Tenant = "acme", Ring = 2 };
            var handle = await WorkflowsHarness.Start(env.Client, start);
            _ = await WorkflowsHarness.Until(handle, static v => v.Task is not null);
            var output = new FactValue("fact:sha256:" + new string('e', 64), new string('e', 64), HeadSha: null);
            var report = new StepCompleted("human:cli", RunToken.From("1.1"), OutcomeName.From("done"), new Dictionary<FactKey, FactValue> { [FactKey.From("fact:output")] = output }, Tokens: 0, StepRuntime.Desk, new string('f', 64));
            _ = await handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(report), new WorkflowUpdateOptions(new string('a', 32)));

            var rows = (await sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 100, ct)).Value
                .Where(static e => e.Envelope.Type.StartsWith("workflow.", StringComparison.Ordinal)).ToList();

            await Finish(handle);

            Assert.Equal(2, rows.Count);
            Assert.All(rows, static e => Assert.Equal(("acme", 2), (e.Envelope.Tenant, e.Envelope.Ring)));
        }, ct);
    }

    [Fact(Timeout = 120_000)]
    public async Task The_instance_id_is_the_one_its_address_derives()
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var start = await WorkflowsHarness.StepStart(sindri, "T-52", ct);
            var handle = await WorkflowsHarness.Start(env.Client, start);

            var view = await WorkflowsHarness.Until(handle, static v => v.Task is not null);
            await Finish(handle);   // before the assertion: a failing one mustn't leave an open activity holding back the server's time skipping

            Assert.Equal(WorkflowInstanceId.Of(start.WorkflowId()), view.Id);
        }, ct);
    }

    /// <summary>Ends the instance by a cancel Update, which leaves no open activity behind to hold back the shared server's time skipping.</summary>
    internal static async Task Finish(WorkflowHandle<RecipeWorkflow, RecipeResult> handle)
    {
        var cancel = new CancelRequested("human:cli", CancelOrigin.Command, new CommandStamp(CommandNonce.From(new string('d', 32)), new string('d', 64)), Sequence: null, Parent: null);
        _ = await handle.ExecuteUpdateAsync(wf => wf.CancelAsync(cancel), new WorkflowUpdateOptions(new string('d', 32)));
    }
}
