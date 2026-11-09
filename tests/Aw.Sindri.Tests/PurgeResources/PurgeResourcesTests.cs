using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Resources;
using Aw.Sindri.Features.PurgeResources;
using Aw.Sindri.Infrastructure.Resources;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.PurgeResources;

/// <summary>'sindri resources purge': archiving keeps a workflow's data; a purge removes it only while the workflow is off and has no run in flight.</summary>
[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class PurgeResourcesTests : IDisposable
{
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("triage");

    private readonly TempHome _home = new();
    private readonly FakeTimeProvider _clock = new();
    private readonly InMemoryWorkflowStore _workflows;
    private readonly InMemoryWorkflowRunStore _runs;
    private readonly PurgeResourcesService _service;

    public PurgeResourcesTests()
    {
        _workflows = new InMemoryWorkflowStore(_clock);
        _runs = new InMemoryWorkflowRunStore(_clock);
        _service = Build();
    }

    public void Dispose() => _home.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string Root => Path.Combine(_home.Path, "resources");

    [Fact]
    public async Task Archiving_a_workflow_keeps_its_data_and_purge_removes_it_only_when_inactive()
    {
        await CreateWorkflow();
        Provision("workflow:triage");
        Provision("workflow:other");
        _ = (await _workflows.Activate(_id, 1, Token)).Value;

        var active = await _service.Handle(new PurgeResourcesRequest("workflow:triage"), Token);
        Assert.Equal("SND-RES-003", active.Error.Code);   // on: refused

        _ = (await _workflows.Archive(_id, Token)).Value;
        var run = new WorkflowRun(WorkflowRunId.Of(_slug, 1, 1), _id, 1, "{\"kind\":\"manual\"}", WorkflowRunStatus.Running, Outputs: null, Error: null, ItemId: null, _clock.GetUtcNow(), EndedAt: null);
        _ = (await _runs.Insert(run, Token)).Value;
        Assert.True(File.Exists(Path.Combine(Root, "workflow", "triage", "state.db")));   // archiving kept the data

        var running = await _service.Handle(new PurgeResourcesRequest("workflow:triage"), Token);
        Assert.Equal("SND-RES-003", running.Error.Code);
        Assert.Contains(run.Id.Value, running.Error.Message, StringComparison.Ordinal);   // names the run
        Assert.True(File.Exists(Path.Combine(Root, "workflow", "triage", "state.db")));

        _ = (await _runs.SetStatus(run.Id, WorkflowRunStatus.Succeeded, outputs: null, error: null, Token)).Value;
        var purged = await _service.Handle(new PurgeResourcesRequest("workflow:triage"), Token);

        Assert.Equal(2, purged.Value.Removed);   // the database and the disk
        Assert.False(Directory.Exists(Path.Combine(Root, "workflow", "triage")));
        Assert.True(File.Exists(Path.Combine(Root, "workflow", "other", "state.db")));   // nobody else's folder was touched
    }

    [Fact]
    public async Task A_malformed_owner_is_refused_and_an_owner_with_nothing_purges_to_zero()
    {
        var bad = await _service.Handle(new PurgeResourcesRequest("../etc"), Token);
        var none = await _service.Handle(new PurgeResourcesRequest("toolkit:nothing"), Token);

        Assert.Equal("SND-RES-006", bad.Error.Code);
        Assert.Equal(0, none.Value.Removed);
    }

    private static PurgeResourcesService Build() => new();

    private async Task CreateWorkflow() =>
        _ = (await _workflows.Create(_id, _slug, new GraphSpec(Version: 1, "Triage", Description: null, Concurrency: null, Resources: null, Nodes: [new GraphNode(NodeId.From("start"), "manual", Title: null, [])], Edges: []), "owner", Token)).Value;

    private void Provision(string owner)
    {
        var parsed = ResourceOwner.Parse(owner).Value;
        var plan = ResourcePlanner.Plan(parsed, new ResourceSpec([new DatabaseResource("state")], [new DiskResource("scratch", "1MiB")], Sandbox: null, []), Root).Value;
        Assert.True(new ResourceStore(Root).Provision(parsed, plan).IsSuccess);
        File.WriteAllText(Path.Combine(Root, parsed.Folder, "scratch", "note.txt"), "kept");
    }
}
