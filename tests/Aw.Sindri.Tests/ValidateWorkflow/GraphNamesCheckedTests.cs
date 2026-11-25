using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Validation;
using Aw.Sindri.Core.Memory;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Host;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Connections;
using Aw.Sindri.Testing.Memory;
using Aw.Sindri.Tests.Graph.Blocks;
using static Aw.Sindri.Tests.Graph.GraphFixtures;

namespace Aw.Sindri.Tests.ValidateWorkflow;

/// <summary>Activation checks the connection, memory and event-kind names a workflow uses against the stores and the connection's service (#3001, #2987).</summary>
[Lane(TestLane.Unit)]
public sealed class GraphNamesCheckedTests
{
    private static readonly CancellationToken _token = TestContext.Current.CancellationToken;

    private static GraphNode EventTrigger(string connection, string kind) =>
        new(NodeId.From("hook"), "connection_event", Title: null, new JsonObject { ["connection"] = connection, ["event"] = kind });

    private static GraphNode Recall(string memory) =>
        new(NodeId.From("recall"), "memory_search", Title: null, new JsonObject { ["memory"] = memory, ["query"] = "last checked" });

    private static async Task<GraphValidation> Activate(GraphSpec spec, IConnectionStore? connections, IMemoryStore? memories)
    {
        var profile = new SindriProfile(1, "thor");
        var profiles = new InMemoryProfiles { Live = new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml") };
        var catalog = GraphBlockRegistry.Standard(new BlockFakes.RecordingNotifyPort(), new BlockFakes.ScriptedModelPort("{}"), more: GraphBlockRegistry.MemoryBlocks(new BlockFakes.RecordingMemoryPort()));
        var validator = await new ProfileWorkflowChecker(profiles, catalog, connections: connections, memories: memories).Validator(_token);
        return validator.Validate(spec, GraphMode.Activate);
    }

    private static Task<GraphValidation> ActivateEvent(string connection, string kind, IConnectionStore? store) =>
        Activate(Spec([EventTrigger(connection, kind), Notify("tell", "hi")], [Edge("hook", "tell")]), store, memories: null);

    private static Task<GraphValidation> ActivateRecall(string memory, IMemoryStore? store) =>
        Activate(Spec([Trigger("daily"), Recall(memory)], [Edge("daily", "recall")]), connections: null, store);

    private static async Task<InMemoryConnectionStore> GithubWork()
    {
        var store = new InMemoryConnectionStore();
        _ = await store.Upsert(ConnectionSamples.Oauth(ConnectionSamples.Name("github", "work")), _token);
        return store;
    }

    private static async Task<InMemoryMemoryStore> NotesMemory()
    {
        var store = new InMemoryMemoryStore();
        _ = await store.Create(new MemoryInfo(MemoryName.Parse("notes").Value, Description: null, CreatedBy: "human", KnownInstants.Epoch), _token);
        return store;
    }

    [Fact]
    public async Task Activate_refuses_a_trigger_naming_a_connection_the_store_lacks_with_GRAPH_NAME_001()
    {
        var result = await ActivateEvent("github:home", "github.pr.merged", await GithubWork());

        var finding = Assert.Single(result.Findings, f => f.Code == "GRAPH-NAME-001");
        Assert.Contains("hook", finding.Nodes, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Activate_does_not_warn_about_a_connection_the_store_has()
    {
        var result = await ActivateEvent("github:work", "github.pr.merged", await GithubWork());

        Assert.DoesNotContain(result.Findings, f => f.Code == "GRAPH-NAME-002");
    }

    [Fact]
    public async Task Activate_refuses_a_memory_block_naming_a_memory_the_store_lacks_with_GRAPH_NAME_001()
    {
        var result = await ActivateRecall("other", await NotesMemory());

        var finding = Assert.Single(result.Findings, f => f.Code == "GRAPH-NAME-001");
        Assert.Contains("recall", finding.Nodes, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Activate_does_not_warn_about_a_memory_the_store_has()
    {
        var result = await ActivateRecall("notes", await NotesMemory());

        Assert.DoesNotContain(result.Findings, f => f.Code == "GRAPH-NAME-002");
    }

    [Fact]
    public async Task Activate_refuses_an_event_kind_the_connections_service_does_not_poll_and_names_its_kinds()
    {
        var result = await ActivateEvent("github:work", "linear.issue.created", await GithubWork());

        Assert.False(result.Passed);
        var finding = Assert.Single(result.Findings, f => f.Field == "config.event");
        Assert.Contains("github.pr.merged", finding.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Activate_with_no_connection_store_still_only_warns_GRAPH_NAME_002()
    {
        var result = await ActivateEvent("github:work", "github.pr.merged", store: null);

        var finding = Assert.Single(result.Findings, f => f.Code == "GRAPH-NAME-002");
        Assert.Equal(GraphSeverity.Warning, finding.Severity);
        Assert.True(result.Passed);
    }

    [Fact]
    public async Task Activate_accepts_an_event_kind_the_connections_service_polls()
    {
        var result = await ActivateEvent("github:work", "github.pr.opened", await GithubWork());

        Assert.DoesNotContain(result.Findings, f => f.Field == "config.event");
    }
}
