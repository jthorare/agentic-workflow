using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Memory;

/// <summary>A workflow's memory nodes reach the real memory slices as 'service:workflow' (agent-first PR 13 follow-up): what one run remembers a later run recalls.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowMemoryPortTests
{
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowRunId _first = WorkflowRunId.From("drift-check@1:1");
    private static readonly WorkflowRunId _second = WorkflowRunId.From("drift-check@1:2");

    private readonly MemoryHarness _h = new();
    private readonly InMemoryWorkflowRunStore _runs;

    public WorkflowMemoryPortTests()
    {
        _runs = new InMemoryWorkflowRunStore(_h.Clock);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private WorkflowMemoryPort Port() => new(_h.Search(), _h.Write(), _h.Forget(), _h.Callers, _runs, _h.Clock);

    private async Task SeedRun(WorkflowRunId run, ItemId? item)
    {
        _ = (await _runs.Insert(new WorkflowRun(run, _id, 1, """{"kind":"manual"}""", WorkflowRunStatus.Running, Outputs: null, Error: null, ItemId: null, KnownInstants.Epoch, EndedAt: null), Token)).Value;
        if (item is { } id)
        {
            _ = (await _runs.SetItem(run, id, Token)).Value;
        }
    }

    [Fact]
    public async Task A_workflow_remembers_a_fact_and_a_later_run_recalls_it()
    {
        await _h.Memory("drift-check");
        await SeedRun(_first, ItemId.From("ITM-00000000000000000000000000000001"));
        await SeedRun(_second, item: null);
        var port = Port();

        var written = await port.Remember(new MemoryToRemember(_first, "drift-check", "Checked api-repo at abc123", ["checks"], ExpiresIn: null), Token);
        var recalled = await port.Recall(new MemoryToRecall("drift-check", "Checked api-repo", Level: null, Limit: null), Token);

        Assert.True(written.IsSuccess, written.IsError ? written.Error.ToString() : null);
        Assert.Equal("added", written.Value.Action);
        Assert.True(recalled.IsSuccess, recalled.IsError ? recalled.Error.ToString() : null);
        Assert.Equal(["Checked api-repo at abc123"], recalled.Value.Facts);
    }

    [Fact]
    public async Task A_remembered_fact_names_its_workflow_run_and_item_and_its_writer_is_the_workflow_service()
    {
        await _h.Memory("drift-check");
        await SeedRun(_first, ItemId.From("ITM-00000000000000000000000000000001"));

        _ = await Port().Remember(new MemoryToRemember(_first, "drift-check", "Checked api-repo at abc123", [], ExpiresIn: TimeSpan.FromDays(30)), Token);

        var stored = Assert.Single(_h.Store.AllFacts);
        Assert.Equal(("drift-check", _first.Value, "ITM-00000000000000000000000000000001"), (stored.Provenance.Workflow, stored.Provenance.Run, stored.Provenance.Item));
        Assert.Equal(_h.Clock.GetUtcNow() + TimeSpan.FromDays(30), stored.ExpiresAt);
        Assert.Contains("service:workflow", stored.WrittenBy, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Forgetting_by_tag_removes_the_facts_and_says_how_many()
    {
        await _h.Memory("drift-check");
        await _h.Hold();
        await SeedRun(_first, item: null);
        var port = Port();
        _ = await port.Remember(new MemoryToRemember(_first, "drift-check", "Stale note", ["stale"], ExpiresIn: null), Token);
        _ = await port.Remember(new MemoryToRemember(_first, "drift-check", "Keep this", ["keep"], ExpiresIn: null), Token);

        var forgotten = await port.Forget(new MemoryToForget("drift-check", Fact: null, Tag: "stale"), Token);

        Assert.Equal(1, forgotten.Value);
        Assert.Equal(["Keep this"], _h.Store.AllFacts.Select(static f => f.Text), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_memory_that_does_not_exist_is_the_slices_error()
    {
        await SeedRun(_first, item: null);

        var recalled = await Port().Recall(new MemoryToRecall("nope", "anything", Level: null, Limit: null), Token);

        Assert.True(recalled.IsError);
    }
}
