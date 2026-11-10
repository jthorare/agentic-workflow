using Aw.Sindri.Core.Events;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Events;

/// <summary>The catalog rows of 'workflow.node.taken-over' and 'workflow.node.handed-back' (#2497); their payload goldens are in <see cref="WorkflowRunEventGoldenTests"/>.</summary>
public sealed class WorkflowNodeControlEventGoldenTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData(EventTypes.WorkflowNodeTakenOver, "workflow.node.taken-over:<run id>:<node id>:<item index>:<command id>")]
    [InlineData(EventTypes.WorkflowNodeHandedBack, "workflow.node.handed-back:<run id>:<node id>:<item index>:<command id>")]
    public void The_events_are_catalogued_with_their_key_shape_and_refresh_the_timeline(string type, string keyShape)
    {
        var entry = Assert.Single(EventCatalog.All, e => e.Type == type);

        Assert.False(entry.Public);
        Assert.Equal(keyShape, entry.KeyShape);
        Expect.Sequence([ApiViews.Timeline], entry.Invalidates);
    }
}
