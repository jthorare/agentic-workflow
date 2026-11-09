using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>A run that works for an item is linked to it in the ledger when it starts (issue 2433).</summary>
public sealed class GraphMirrorRunLinkTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static WorkflowRun Run(ItemId? item) =>
        new(WorkflowRunId.From("nightly@1:1"), WorkflowId.From("WF-" + new string('1', 32)), 1, """{"kind":"manual"}""", WorkflowRunStatus.Running, Outputs: null, Error: null, item, KnownInstants.Epoch, EndedAt: null);

    [Fact, Trait("Lane", "unit")]
    public async Task A_run_started_with_an_item_appends_item_linked_for_the_run()
    {
        var harness = new SliceHarness();
        _ = await harness.HoldLease();
        var mirror = new GraphMirror(harness.Store, harness.Held, harness.Events);

        await mirror.AppendStarted(Run(A.FirstItemId), Token);

        var linked = Assert.Single(harness.Store.Events, static e => e.Envelope.Type == EventTypes.ItemLinked);
        var payload = harness.Events.Payload<ItemLinked>(linked).Value;
        Assert.Equal((A.FirstItemId, true), (payload.Item, payload.Record.Value.StartsWith("sindri:run/", StringComparison.Ordinal)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_run_started_with_no_item_appends_no_item_linked()
    {
        var harness = new SliceHarness();
        _ = await harness.HoldLease();
        var mirror = new GraphMirror(harness.Store, harness.Held, harness.Events);

        await mirror.AppendStarted(Run(item: null), Token);

        Assert.DoesNotContain(harness.Store.Events, static e => e.Envelope.Type == EventTypes.ItemLinked);
    }
}
