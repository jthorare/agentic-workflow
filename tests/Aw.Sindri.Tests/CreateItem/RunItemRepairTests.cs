using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.CreateItem;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.CreateItem;

/// <summary>
/// The repair pass for a 'create_item' node whose run link or run item was not written (#2588): it reads the node outputs of each run and writes what is missing, per item the 'item.linked' row
/// and per run the earliest made item when the run has none. A second pass writes nothing.
/// </summary>
public sealed class RunItemRepairTests
{
    private static readonly WorkflowId _workflow = WorkflowId.From("WF-" + new string('1', 32));
    private static readonly WorkflowRunId _run = WorkflowRunId.From("nightly@1:1");
    private static readonly NodeId _node = NodeId.From("make");

    private readonly SliceHarness _h = new();
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly SetItemFailingRunStore _runs;
    private readonly InMemoryWorkflowStore _workflows;

    public RunItemRepairTests()
    {
        _runs = new SetItemFailingRunStore(new InMemoryWorkflowRunStore(_clock));
        _workflows = new InMemoryWorkflowStore(_clock);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private WorkflowItemMaker Maker(bool linkRefused) =>
        new(new CreateItemService(_h.Profiles, _h.Held, linkRefused ? new LinkRefusingLedger(_h.Store) : _h.Store, _h.Events, _h.Ids, _h.Callers), _h.Callers, _runs);

    private RunItemRepair Repair() => new(_workflows, _runs, _h.Store, _h.Held, _h.Events, _h.Callers, _h.Ids);

    private async Task Seed(WorkflowRunStatus status = WorkflowRunStatus.Succeeded)
    {
        _ = await _h.HoldLease();
        var spec = new GraphSpec(Version: 1, Name: "Nightly", Description: null, Concurrency: null, Resources: null, Nodes: [new GraphNode(NodeId.From("start"), "manual", Title: null, [])], Edges: []);
        _ = (await _workflows.Create(_workflow, WorkflowSlug.From("nightly"), spec, "owner", Token)).Value;
        _ = (await _runs.Insert(new WorkflowRun(_run, _workflow, 1, """{"kind":"manual"}""", status, Outputs: null, Error: null, ItemId: null, KnownInstants.Epoch, EndedAt: null), Token)).Value;
    }

    /// <summary>Makes an item for the run the way the node does and records the node's output, as the node would.</summary>
    private async Task<ItemId> Make(string title, int itemIndex, bool linkRefused)
    {
        var made = await Maker(linkRefused).Create(new ItemToCreate(_run, title, Repo: null, Body: null, LinearIssue: null), Token);
        Assert.True(made.IsSuccess, made.IsError ? made.Error.ToString() : null);
        _ = (await _runs.RecordNodeRun(new NodeRun(_run, _node, itemIndex, NodeRunStatus.Succeeded, Input: null, $$"""{"itemId":"{{made.Value.ItemId.Value}}"}""", Truncated: false, Error: null, Usage: null), Token)).Value;
        return made.Value.ItemId;
    }

    private bool Linked(ItemId item) => _h.Store.Events.Any(e => e.Envelope.Type == EventTypes.ItemLinked && e.Item == item.Value
        && _h.Events.Payload<ItemLinked>(e).Value.Record.Value == "sindri:run/" + _run.Value);

    private async Task<ItemId?> RunItem() => (await _runs.Get(_run, Token)).Value.ItemId;

    [Fact, Trait("Lane", "unit")]
    public async Task A_refused_link_is_written_by_the_pass()
    {
        await Seed();
        var item = await Make("one", 0, linkRefused: true);
        Assert.False(Linked(item));

        var repaired = await Repair().RepairAll(Token);

        Assert.True(repaired.IsSuccess, repaired.IsError ? repaired.Error.ToString() : null);
        Assert.True(Linked(item));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_run_item_write_is_made_by_the_pass()
    {
        await Seed();
        _runs.Fail = true;
        var item = await Make("one", 0, linkRefused: false);
        _runs.Fail = false;
        Assert.Null(await RunItem());

        _ = await Repair().RepairAll(Token);

        Assert.Equal(item, await RunItem());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Two_made_items_with_no_run_item_end_with_the_first_made_item_and_both_linked()
    {
        await Seed();
        _runs.Fail = true;
        var first = await Make("one", 1, linkRefused: true);
        var second = await Make("two", 2, linkRefused: true);
        _runs.Fail = false;

        _ = await Repair().RepairAll(Token);

        Assert.Equal(first, await RunItem());   // the live path's SetItem is first-wins, so the earliest made item is the run's
        Assert.True(Linked(first));
        Assert.True(Linked(second));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_run_that_has_an_item_still_gets_the_missing_link_of_a_later_item()
    {
        await Seed();
        var first = await Make("one", 1, linkRefused: false);
        var second = await Make("two", 2, linkRefused: true);
        Assert.Equal(first, await RunItem());
        Assert.False(Linked(second));

        _ = await Repair().RepairAll(Token);

        Assert.True(Linked(second));
        Assert.Equal(first, await RunItem());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_second_pass_writes_nothing()
    {
        await Seed();
        _runs.Fail = true;
        _ = await Make("one", 1, linkRefused: true);
        _ = await Make("two", 2, linkRefused: true);
        _runs.Fail = false;
        _ = await Repair().RepairAll(Token);
        var rows = _h.Store.Events.Count;
        var setItems = _runs.SetItemCalls;

        var again = await Repair().RepairAll(Token);

        Assert.True(again.IsSuccess, again.IsError ? again.Error.ToString() : null);
        Assert.Equal(0, again.Value);
        Assert.Equal((rows, setItems), (_h.Store.Events.Count, _runs.SetItemCalls));
    }
}
