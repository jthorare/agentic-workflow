using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Tests.Catalog;

public sealed class NodeAgentCatalogTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_node_agent_event_is_catalogued_publicly_with_ids_only_under_its_key_shape()
    {
        var entry = Assert.Single(EventCatalog.All, e => e.Type == "workflow.node.agent.changed");

        Assert.True(entry.Public);
        Assert.Equal(typeof(WorkflowNodePublic), entry.PublicPayload);   // ids only: state, tool and branch are read from the view
        Assert.Equal("workflow.node.agent.changed:<run id>:<node id>:<item index>:<sequence>", entry.KeyShape);
        Assert.Equal("workflow.node.agent.changed", EventTypes.WorkflowNodeAgentChanged);
    }
}
