using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Resources;
using Aw.Sindri.Features.ActivateWorkflow;
using Aw.Sindri.Tests.WorkflowWrites;
using static Aw.Sindri.Tests.WorkflowWrites.WorkflowWriteRig;

namespace Aw.Sindri.Tests.ActivateWorkflow;

/// <summary>'sindri wf activate': a revision that passes the activation checks becomes the one triggers run.</summary>
[Lane(TestLane.Unit)]
public sealed class ActivateWorkflowServiceTests : IDisposable
{
    private readonly WorkflowWriteRig _rig = new();
    private readonly RecordingProvisioner _provisioner = new();
    private readonly ActivateWorkflowService _service;

    public ActivateWorkflowServiceTests()
    {
        _service = new ActivateWorkflowService(_rig.Workflows, _rig.Checker, _rig.Log, _rig.Harness.Callers, _provisioner);
    }

    private static GraphSpec WithDatabase() => SampleGraph() with { Resources = new GraphResources(new GraphDatabase("state"), Sandbox: null) };

    public void Dispose() => _rig.Dispose();

    [Fact]
    public async Task The_newest_revision_is_activated_by_default_and_the_event_is_written()
    {
        await _rig.Seed();
        _ = (await _rig.Workflows.Save(Id, 1, SampleGraph(), "owner", Token)).Value;

        var result = await _service.Handle(new ActivateWorkflowRequest(Slug, Revision: null, "session:s1"), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Revision);
        Assert.Equal(2, (await _rig.Workflows.Get(Id, Token)).Value.ActiveRevision);
        _ = Assert.Single(_rig.Recorded(EventTypes.WorkflowActivated));
    }

    [Fact]
    public async Task A_named_revision_is_activated()
    {
        await _rig.Seed();
        _ = (await _rig.Workflows.Save(Id, 1, SampleGraph(), "owner", Token)).Value;

        var result = await _service.Handle(new ActivateWorkflowRequest(Slug, 1, "owner"), Token);

        Assert.Equal(1, result.Value.Revision);
    }

    [Fact]
    public async Task A_graph_with_errors_is_refused_with_its_findings_and_stays_off()
    {
        await _rig.Seed();
        const string noTrigger = "{\"version\":1,\"name\":\"No trigger\",\"nodes\":[{\"id\":\"tell\",\"type\":\"notify\",\"config\":{\"title\":\"x\"}}],\"edges\":[]}";
        _ = (await _rig.Workflows.Save(Id, 1, GraphJson.Parse(noTrigger).Value, "owner", Token)).Value;

        var result = await _service.Handle(new ActivateWorkflowRequest(Slug, Revision: null, "owner"), Token);

        Assert.Equal(SindriErrors.GraphFindings.Code, result.Error.Code);
        Assert.Null((await _rig.Workflows.Get(Id, Token)).Value.ActiveRevision);
        Assert.Empty(_rig.Recorded(EventTypes.WorkflowActivated));
    }

    [Fact]
    public async Task An_unknown_workflow_or_revision_is_not_found_and_an_archived_workflow_is_refused()
    {
        await _rig.Seed();
        var missing = await _service.Handle(new ActivateWorkflowRequest(Core.Graph.Ids.WorkflowSlug.From("nope"), Revision: null, "owner"), Token);
        var revision = await _service.Handle(new ActivateWorkflowRequest(Slug, 9, "owner"), Token);
        _ = (await _rig.Workflows.Archive(Id, Token)).Value;

        var archived = await _service.Handle(new ActivateWorkflowRequest(Slug, Revision: null, "owner"), Token);

        Assert.Equal(SindriErrors.GraphNotFound.Code, missing.Error.Code);
        Assert.Equal(SindriErrors.GraphNotFound.Code, revision.Error.Code);
        Assert.Equal(SindriErrors.GraphArchived.Code, archived.Error.Code);
    }

    [Fact]
    public async Task Activating_a_revision_that_declares_a_database_provisions_it_for_the_workflow_owner()
    {
        await _rig.Seed();
        _ = (await _rig.Workflows.Save(Id, 1, WithDatabase(), "owner", Token)).Value;

        var result = await _service.Handle(new ActivateWorkflowRequest(Slug, Revision: null, "owner"), Token);

        Assert.True(result.IsSuccess);
        var (owner, declared) = Assert.Single(_provisioner.Calls);
        Assert.Equal("workflow:drift-check", owner.ToString());
        Assert.Equal("state", Assert.Single(declared.Databases).Name);
    }

    [Fact]
    public async Task A_revision_with_no_resources_never_asks_the_provisioner()
    {
        await _rig.Seed();

        var result = await _service.Handle(new ActivateWorkflowRequest(Slug, Revision: null, "owner"), Token);

        Assert.True(result.IsSuccess);
        Assert.Empty(_provisioner.Calls);
    }

    [Fact]
    public async Task A_provisioning_failure_refuses_the_activate_and_the_workflow_stays_off()
    {
        await _rig.Seed();
        _ = (await _rig.Workflows.Save(Id, 1, WithDatabase(), "owner", Token)).Value;
        _provisioner.Fail = SindriErrors.ResourceDiskOverSize.With("Disk 'scratch' holds 4096 bytes, more than its limit of 1024 bytes.");

        var result = await _service.Handle(new ActivateWorkflowRequest(Slug, Revision: null, "owner"), Token);

        Assert.Equal("SND-RES-001", result.Error.Code);
        Assert.Null((await _rig.Workflows.Get(Id, Token)).Value.ActiveRevision);
        Assert.Empty(_rig.Recorded(EventTypes.WorkflowActivated));
    }

    private sealed class RecordingProvisioner : IResourceProvisioner
    {
        public List<(ResourceOwner Owner, ResourceSpec Spec)> Calls { get; } = [];

        public Error? Fail { get; set; }

        public ValueTask<Result<ProvisionedResources>> Ensure(ResourceOwner owner, ResourceSpec spec, CancellationToken cancellationToken)
        {
            Calls.Add((owner, spec));
            return ValueTask.FromResult(Fail is { } error ? Results.Failure<ProvisionedResources>(error) : Results.Success(ProvisionedResources.None));
        }
    }
}
