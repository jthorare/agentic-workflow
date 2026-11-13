using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Features.GetWorkflow;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Graph.Blocks;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.WorkflowsPageP1e;

/// <summary>The Title rule (Workflows page spec decision 6, W11, E5, #3074): the catalog Title is the one name of a step kind; GET /sindri/workflows/{slug} carries it as the node's kind title, never the type id or the node id.</summary>
[Lane(TestLane.Unit)]
public sealed class NodeKindTitleTests
{
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("flagged");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<IReadOnlyList<NodeSummary>> Summaries()
    {
        var workflows = new InMemoryWorkflowStore(new FakeTimeProvider());
        var catalog = GraphBlockRegistry.Standard(new BlockFakes.RecordingNotifyPort(), new BlockFakes.ScriptedModelPort("{}"));
        var spec = new GraphSpec(Version: 1, "Flagged", Description: null, Concurrency: null, Resources: null,
            Nodes:
            [
                new GraphNode(NodeId.From("start"), "manual", Title: null, []),
                new GraphNode(NodeId.From("gate"), "condition", "Flagged?", new JsonObject { ["when"] = "start.payload" }),
                new GraphNode(NodeId.From("tell"), "notify", "Tell me", new JsonObject { ["title"] = "x" }),
            ],
            Edges: []);
        _ = (await workflows.Create(_id, _slug, spec, "owner", Token)).Value;

        var shown = await new GetWorkflowService(workflows, catalog).Handle(new GetWorkflowRequest(_slug), Token);

        Assert.True(shown.IsSuccess);
        return shown.Value.Summaries!;
    }

    [Fact]
    public async Task The_condition_step_is_named_If_and_the_notify_step_Send_notification_whatever_the_node_is_called()
    {
        var summaries = await Summaries();

        Assert.Equal("If", summaries.Single(static s => s.Node == "gate").KindTitle);
        Assert.Equal("Send notification", summaries.Single(static s => s.Node == "tell").KindTitle);
    }

    [Fact]
    public async Task Every_kind_title_is_the_catalog_Title_and_none_is_a_type_id_or_a_node_id()
    {
        var summaries = await Summaries();

        var catalog = GraphBlockRegistry.Standard(new BlockFakes.RecordingNotifyPort(), new BlockFakes.ScriptedModelPort("{}")).Types;
        Assert.All(summaries, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.KindTitle), $"node '{s.Node}' has no kind title");
            Assert.DoesNotContain(s.KindTitle, catalog.Keys, StringComparer.Ordinal);
            Assert.NotEqual(s.Node, s.KindTitle, StringComparer.Ordinal);
        });
    }
}
