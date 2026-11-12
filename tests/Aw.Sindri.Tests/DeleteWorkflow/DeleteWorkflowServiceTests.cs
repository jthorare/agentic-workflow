using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Resources;
using Aw.Sindri.Features.DeleteWorkflow;
using Aw.Sindri.Infrastructure.Resources;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.WorkflowWrites;
using static Aw.Sindri.Tests.WorkflowWrites.WorkflowWriteRig;

namespace Aw.Sindri.Tests.DeleteWorkflow;

/// <summary>'sindri wf delete': the workflow is archived (hidden and off for good); nothing is removed.</summary>
[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class DeleteWorkflowServiceTests : IDisposable
{
    private readonly WorkflowWriteRig _rig = new();
    private readonly TempHome _home = new();
    private readonly InMemoryWorkflowRunStore _runs;
    private readonly DeleteWorkflowService _service;

    public DeleteWorkflowServiceTests()
    {
        _runs = new InMemoryWorkflowRunStore(_rig.Harness.Clock);
        _service = new DeleteWorkflowService(_rig.Workflows, _runs, new ResourceFiles(Root), _rig.Log);
    }

    private string Root => Path.Combine(_home.Path, "resources");

    public void Dispose()
    {
        _rig.Dispose();
        _home.Dispose();
    }

    [Fact]
    public async Task An_archived_workflow_leaves_the_list_turns_off_and_writes_the_event()
    {
        await _rig.Seed();

        var result = await _service.Handle(new DeleteWorkflowRequest(Slug, "session:s1"), Token);

        Assert.True(result.IsSuccess);
        Assert.Empty((await _rig.Workflows.List(includeArchived: false, Token)).Value);
        var record = (await _rig.Workflows.Get(Id, Token)).Value;
        Assert.Null(record.ActiveRevision);
        _ = Assert.NotNull(record.ArchivedAt);
        Assert.Equal($"workflow.archived:{Id.Value}", Assert.Single(_rig.Recorded(EventTypes.WorkflowArchived)).IdempotencyKey);
    }

    [Fact]
    public async Task An_unknown_workflow_is_not_found_and_an_archived_one_is_refused()
    {
        await _rig.Seed();
        var unknown = await _service.Handle(new DeleteWorkflowRequest(Core.Graph.Ids.WorkflowSlug.From("nope"), "owner"), Token);
        _ = await _service.Handle(new DeleteWorkflowRequest(Slug, "owner"), Token);

        var again = await _service.Handle(new DeleteWorkflowRequest(Slug, "owner"), Token);

        Assert.Equal(SindriErrors.GraphNotFound.Code, unknown.Error.Code);
        Assert.Equal(SindriErrors.GraphArchived.Code, again.Error.Code);
    }

    [Fact]
    public async Task Delete_refuses_an_active_workflow_with_114()
    {
        await _rig.Seed();
        _ = (await _rig.Workflows.Activate(Id, 1, Token)).Value;

        var result = await _service.Handle(new DeleteWorkflowRequest(Slug, "owner"), Token);

        Assert.Equal(SindriErrors.GraphActive.Code, result.Error.Code);
        Assert.Equal(1, (await _rig.Workflows.Get(Id, Token)).Value.ActiveRevision);
        Assert.Null((await _rig.Workflows.Get(Id, Token)).Value.ArchivedAt);
    }

    [Theory]
    [InlineData(WorkflowRunStatus.Queued)]
    [InlineData(WorkflowRunStatus.Running)]
    [InlineData(WorkflowRunStatus.Waiting)]
    public async Task Delete_refuses_a_workflow_with_a_run_in_flight_with_115(WorkflowRunStatus status)
    {
        await _rig.Seed();
        await AddRun(1, status);

        var result = await _service.Handle(new DeleteWorkflowRequest(Slug, "owner"), Token);

        Assert.Equal(SindriErrors.GraphRunInFlight.Code, result.Error.Code);
        Assert.Null((await _rig.Workflows.Get(Id, Token)).Value.ArchivedAt);
    }

    [Fact]
    public async Task Delete_purges_the_workflow_and_node_resources_and_reports_runs_kept_and_bytes_freed()
    {
        await _rig.Seed();
        await AddRun(1, WorkflowRunStatus.Succeeded);
        await AddRun(2, WorkflowRunStatus.Failed);
        var expected = Provision("workflow:drift-check") + Provision("node:drift-check/a");
        _ = Provision("workflow:other");

        var result = await _service.Handle(new DeleteWorkflowRequest(Slug, "owner"), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.RunsKept);
        Assert.True(expected > 0);
        Assert.Equal(expected, result.Value.BytesFreed);
        Assert.Empty(result.Value.ResourcesStayed);
        Assert.False(Directory.Exists(Path.Combine(Root, "workflow", "drift-check")));
        Assert.False(Directory.Exists(Path.Combine(Root, "node", "drift-check")));
        Assert.True(Directory.Exists(Path.Combine(Root, "workflow", "other")));   // nobody else's folder was touched
    }

    [Fact]
    public async Task A_toolkit_with_the_same_name_keeps_its_resources_and_is_not_counted()
    {
        await _rig.Seed();
        var expected = Provision("workflow:drift-check");
        var toolkit = Provision("toolkit:drift-check");
        Assert.True(toolkit > 0);

        var result = await _service.Handle(new DeleteWorkflowRequest(Slug, "owner"), Token);

        Assert.Equal(expected, result.Value.BytesFreed);
        Assert.Empty(result.Value.ResourcesStayed);
        Assert.True(Directory.Exists(Path.Combine(Root, "toolkit", "drift-check")));
    }

    [Fact]
    public async Task A_workflow_with_no_resources_and_no_runs_reports_zero()
    {
        await _rig.Seed();

        var result = await _service.Handle(new DeleteWorkflowRequest(Slug, "owner"), Token);

        Assert.Equal(0, result.Value.RunsKept);
        Assert.Equal(0, result.Value.BytesFreed);
    }

    [Fact]
    public async Task A_purge_that_fails_keeps_the_archive_and_names_what_stayed()
    {
        Assert.SkipWhen(Environment.IsPrivilegedProcess, "a privileged process can delete a read-only folder");
        await _rig.Seed();
        _ = Provision("workflow:drift-check");
        var disk = Path.Combine(Root, "workflow", "drift-check", "scratch");
        File.SetUnixFileMode(disk, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            var result = await _service.Handle(new DeleteWorkflowRequest(Slug, "owner"), Token);

            Assert.True(result.IsSuccess);
            Assert.Equal("workflow:drift-check", Assert.Single(result.Value.ResourcesStayed));
            _ = Assert.NotNull((await _rig.Workflows.Get(Id, Token)).Value.ArchivedAt);
        }
        finally
        {
            File.SetUnixFileMode(disk, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private async Task AddRun(int occurrence, WorkflowRunStatus status)
    {
        var now = _rig.Harness.Clock.GetUtcNow();
        var run = new WorkflowRun(WorkflowRunId.Of(Slug, 1, occurrence), Id, 1, "{\"kind\":\"manual\"}", status, Outputs: null, Error: null, ItemId: null, now, EndedAt: null);
        _ = (await _runs.Insert(run, Token)).Value;
    }

    /// <summary>Provisions a database and a disk for the owner and returns the bytes they hold.</summary>
    private long Provision(string owner)
    {
        var parsed = ResourceOwner.Parse(owner).Value;
        var plan = ResourcePlanner.Plan(parsed, new ResourceSpec([new DatabaseResource("state")], [new DiskResource("scratch", "1MiB")], Sandbox: null, []), Root).Value;
        Assert.True(new ResourceStore(Root).Provision(parsed, plan).IsSuccess);
        File.WriteAllText(Path.Combine(Root, parsed.Folder, "scratch", "note.txt"), "kept");
        return new ResourceFiles(Root).List(parsed).Value.Sum(static e => e.SizeBytes);
    }
}
