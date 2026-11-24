using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Views.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>One icon per block kind on a workflow card (#3684, cards.png): every kind has its own shape, and an unknown kind gets the plain square.</summary>
[Lane(TestLane.Unit)]
public sealed class GraphBlockIconsTests
{
    // The block types of the catalog (Aw.Sindri.Core/Graph/Blocks/*Block.cs).
    private static readonly string[] _catalog =
    [
        "manual", "schedule", "connection_event", "code", "condition", "http", "notify", "wait", "collect", "prompt", "create_item",
        "run_agent", "run_workflow", "for_each", "call_tool", "ask_owner", "only_new", "memory_search", "memory_write", "memory_forget",
    ];

    [Fact]
    public void Every_catalog_kind_has_an_icon_and_no_two_kinds_share_one()
    {
        Assert.All(_catalog, type => Assert.Contains(type, GraphBlockIcons.Known, StringComparer.Ordinal));
        Assert.Equal(_catalog.Length, _catalog.Select(GraphBlockIcons.PathFor).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void An_unknown_kind_gets_the_plain_square_not_another_kinds_icon()
    {
        var square = GraphBlockIcons.PathFor("made_up_kind");

        Assert.DoesNotContain(square, _catalog.Select(GraphBlockIcons.PathFor), StringComparer.Ordinal);
    }
}
