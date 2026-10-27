using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Client;
using Temporalio.Exceptions;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The 'rewind' Update on a real workflow (#1270): an instance that moved on from 'export' to 'import' goes back to 'export' with a new run and its facts
/// kept; a target that ends the instance is refused by the validator, recording nothing. Modelled on <see cref="PhaseFiveSignalTests"/>.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(nameof(WallClockAlone))]
public sealed class RewindUpdateTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>, IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 120_000)]
    public async Task A_rewind_goes_back_to_export_with_a_new_run_and_the_facts_kept_and_refuses_a_terminal_target()
    {
        var ct = TestContext.Current.CancellationToken;
        _harness.Recipe("workflow.dev-desk-round-trip", WorkflowsHarness.Shipped("dev/desk-round-trip.yaml"));
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(server.Environment.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var approved = await sindri.GetRequiredService<IApprovedRecipes>().Latest(RecipeId.From("workflow.dev-desk-round-trip"), ct);
            var request = new FactValue("fact:sha256:" + new string('a', 64), new string('a', 64), HeadSha: null);
            var start = new InstanceStart(approved.Value.Version, ItemKey.From("T-9"), Attempt: 1, new Dictionary<FactKey, InputFact> { [FactKey.From("fact:request")] = new InputFact("request.txt", request) }, Tenant: "local", Ring: 0) { StartedAt = RealTime.NowOffset };
            var handle = await WorkflowsHarness.Start(server.Environment.Client, start);
            var export = await WorkflowsHarness.Until(handle, static v => v.State == "export" && v.Run is not null);
            var output = new FactValue("fact:sha256:" + new string('b', 64), new string('b', 64), HeadSha: null);
            var report = new StepCompleted("human:cli", export.Run!.Value, OutcomeName.From("done"), new Dictionary<FactKey, FactValue> { [FactKey.From("fact:output")] = output }, Tokens: 0, StepRuntime.Desk, new string('f', 64));
            _ = await handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(report), new WorkflowUpdateOptions(new string('1', 32)));
            var imported = await WorkflowsHarness.Until(handle, static v => v.State == "import" && v.Run is not null);

            var back = await handle.ExecuteUpdateAsync(wf => wf.RewindAsync(new RewindRequested("human:cli", Stamp('2'), "export")), new WorkflowUpdateOptions(new string('2', 32)));
            var refused = await Assert.ThrowsAsync<WorkflowUpdateFailedException>(() => handle.ExecuteUpdateAsync(wf => wf.RewindAsync(new RewindRequested("human:cli", Stamp('3'), "done")), new WorkflowUpdateOptions(new string('3', 32))));

            Assert.Equal("2.1", imported.Run?.Value);
            Assert.Equal("export", back.Instance.State);
            Assert.Equal("3.1", back.Instance.Run?.Value);
            Assert.Contains("done", refused.InnerException!.Message, StringComparison.Ordinal);
            Assert.Equal("export", (await WorkflowsHarness.Until(handle, static v => v.State == "export")).State);
            await handle.TerminateAsync("test over");
        }, ct);
    }

    private static CommandStamp Stamp(char digit) => new(CommandNonce.From(new string(digit, 32)), new string('0', 64));
}
