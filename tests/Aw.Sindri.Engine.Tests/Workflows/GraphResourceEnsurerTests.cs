using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Resources;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The step before a node runs that provisions the workflow's resources (agent-first PR 14).</summary>
[Lane(TestLane.Unit)]
public sealed class GraphResourceEnsurerTests
{
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static GraphSpec Spec(GraphResources? resources) => new(Version: 1, "Resources test", Description: null, Concurrency: null, resources, [], []);

    private static async Task<InMemoryWorkflowStore> StoreWithTriage()
    {
        var store = new InMemoryWorkflowStore(new FakeTimeProvider(KnownInstants.Epoch));
        _ = await store.Create(_id, WorkflowSlug.From("triage"), Spec(resources: null), "test", Token);
        return store;
    }

    [Fact]
    public async Task A_spec_with_no_resources_gets_none_and_never_asks_the_provisioner()
    {
        var provisioner = new RecordingProvisioner();

        var result = await new GraphResourceEnsurer(provisioner, await StoreWithTriage()).Ensure(_id, Spec(resources: null), Token);

        Assert.Empty(result.Value.Env);
        Assert.Empty(provisioner.Calls);
    }

    [Fact]
    public async Task A_declared_database_is_provisioned_for_the_workflow_owner_and_its_env_is_returned()
    {
        var provisioner = new RecordingProvisioner();
        var spec = Spec(new GraphResources(new GraphDatabase("state"), new GraphSandbox(Memory: "2g", Wall: "30m")));

        var result = await new GraphResourceEnsurer(provisioner, await StoreWithTriage()).Ensure(_id, spec, Token);

        var (owner, declared) = Assert.Single(provisioner.Calls);
        Assert.Equal("workflow:triage", owner.ToString());
        Assert.Equal("state", Assert.Single(declared.Databases).Name);
        Assert.Equal(("2g", "30m"), (declared.Sandbox?.Memory, declared.Sandbox?.Wall));
        Assert.Equal("/resources/state.db", result.Value.Env["SINDRI_DB_STATE"]);
    }

    [Fact]
    public async Task A_provisioner_failure_comes_back_as_the_error()
    {
        var provisioner = new RecordingProvisioner { Fail = SindriErrors.ResourceDiskOverSize.With("Disk 'scratch' holds 4096 bytes, more than its limit of 1024 bytes.") };

        var result = await new GraphResourceEnsurer(provisioner, await StoreWithTriage()).Ensure(_id, Spec(new GraphResources(new GraphDatabase("state"), Sandbox: null)), Token);

        Assert.Equal("SND-RES-001", result.Error.Code);
    }

    [Fact]
    public async Task A_build_with_no_provisioner_gets_none()
    {
        var result = await new GraphResourceEnsurer(provisioner: null, await StoreWithTriage()).Ensure(_id, Spec(new GraphResources(new GraphDatabase("state"), Sandbox: null)), Token);

        Assert.Empty(result.Value.Env);
    }

    private sealed class RecordingProvisioner : IResourceProvisioner
    {
        public List<(ResourceOwner Owner, ResourceSpec Spec)> Calls { get; } = [];

        public Error? Fail { get; init; }

        public ValueTask<Result<ProvisionedResources>> Ensure(ResourceOwner owner, ResourceSpec spec, CancellationToken cancellationToken)
        {
            Calls.Add((owner, spec));
            return ValueTask.FromResult(Fail is { } error
                ? Results.Failure<ProvisionedResources>(error)
                : Results.Success(new ProvisionedResources(new Dictionary<string, string>(StringComparer.Ordinal) { ["SINDRI_DB_STATE"] = "/resources/state.db" }, [], Limits: null)));
        }
    }
}
