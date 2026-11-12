using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Resources;
using Aw.Sindri.Features.PurgeResources;
using Aw.Sindri.Infrastructure.Resources;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.PurgeResources;

/// <summary>#2941: a purge that deleted something appends one private 'resource.purged' event carrying the caller's actor; a refused, empty or failed purge appends none.</summary>
[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class PurgeResourcesLedgerTests : IDisposable
{
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("triage");

    private readonly TempHome _home = new();
    private readonly FakeTimeProvider _clock = new();
    private readonly SliceHarness _slice = new();
    private readonly InMemoryWorkflowStore _workflows;
    private readonly InMemoryWorkflowRunStore _runs;

    public PurgeResourcesLedgerTests()
    {
        _workflows = new InMemoryWorkflowStore(_clock);
        _runs = new InMemoryWorkflowRunStore(_clock);
    }

    public void Dispose() => _home.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string Root => Path.Combine(_home.Path, "resources");

    [Fact]
    public async Task A_successful_purge_appends_exactly_one_resource_purged_with_owner_counts_and_bytes()
    {
        _ = await _slice.HoldLease();
        var caller = A.Caller().Launched("LCH-0000000000000000000000000000ff01").Build();
        using var entered = _slice.Callers.Enter(caller);
        await CreateWorkflow();
        Provision("workflow:triage");
        var folder = Path.Combine(Root, "workflow", "triage");
        await File.WriteAllTextAsync(Path.Combine(folder, "notes.txt"), "stray", Token);   // the stray file: not a database, not a folder
        var expected = new FileInfo(Path.Combine(folder, "state.db")).Length + new FileInfo(Path.Combine(folder, "scratch", "note.txt")).Length + new FileInfo(Path.Combine(folder, "notes.txt")).Length;
        _ = (await _workflows.Archive(_id, Token)).Value;

        var purged = await Build().Handle(new PurgeResourcesRequest("workflow:triage"), Token);

        Assert.Equal(3, purged.Value.Removed);
        var recorded = Assert.Single(_slice.Store.Events, static e => e.Envelope.Type == EventTypes.ResourcePurged);
        var payload = _slice.Events.Payload<ResourcePurged>(recorded).Value;
        Assert.Equal("workflow:triage", payload.Owner);
        Assert.Equal(1, payload.Databases);
        Assert.Equal(1, payload.Disks);
        Assert.Equal(1, payload.StrayFiles);
        Assert.Equal(expected, payload.BytesFreed);
        Assert.Equal(caller.Actor, recorded.Envelope.Actor);
    }

    [Fact]
    public async Task A_refused_purge_appends_no_resource_purged()
    {
        _ = await _slice.HoldLease();
        await CreateWorkflow();
        Provision("workflow:triage");
        _ = (await _workflows.Activate(_id, 1, Token)).Value;

        var active = await Build().Handle(new PurgeResourcesRequest("workflow:triage"), Token);
        var bad = await Build().Handle(new PurgeResourcesRequest("../etc"), Token);

        Assert.Equal("SND-RES-003", active.Error.Code);
        Assert.Equal("SND-RES-006", bad.Error.Code);
        Assert.DoesNotContain(_slice.Store.Events, static e => e.Envelope.Type == EventTypes.ResourcePurged);
    }

    [Fact]
    public async Task A_purge_of_an_owner_with_nothing_appends_no_event()
    {
        _ = await _slice.HoldLease();

        var none = await Build().Handle(new PurgeResourcesRequest("toolkit:nothing"), Token);

        Assert.Equal(0, none.Value.Removed);
        Assert.DoesNotContain(_slice.Store.Events, static e => e.Envelope.Type == EventTypes.ResourcePurged);
    }

    [Fact]
    public async Task A_purge_whose_delete_fails_appends_no_event()
    {
        Assert.SkipWhen(Environment.IsPrivilegedProcess, "a privileged process can delete a read-only folder");
        _ = await _slice.HoldLease();
        await CreateWorkflow();
        Provision("workflow:triage");
        _ = (await _workflows.Archive(_id, Token)).Value;
        var disk = Path.Combine(Root, "workflow", "triage", "scratch");
        File.SetUnixFileMode(disk, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            var failed = await Build().Handle(new PurgeResourcesRequest("workflow:triage"), Token);

            Assert.Equal("SND-RES-008", failed.Error.Code);
            Assert.DoesNotContain(_slice.Store.Events, static e => e.Envelope.Type == EventTypes.ResourcePurged);
        }
        finally
        {
            File.SetUnixFileMode(disk, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private PurgeResourcesService Build() => new(new ResourceFiles(Root), _workflows, _runs, new ResourceEvents(_slice.Held, _slice.Store, _slice.Events, _clock));

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
