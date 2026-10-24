using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.ListNeighbors;

public sealed class ListNeighborsTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Theory, Trait("Lane", "unit")]
    [InlineData(5, 5)]
    [InlineData(99, 8)]
    public async Task A_depth_past_the_old_cap_is_walked_and_past_8_is_read_as_8(int depth, int shown)
    {
        _ = await _world.ImportFixture();

        var view = (await _world.Engine.Neighbors("thing:pane-layout", depth, MapWorld.Human, MapWorld.None)).Value;

        Assert.Equal(shown, view.Depth);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Neighbors_default_to_depth_1_with_the_edges_among_them()
    {
        _ = await _world.ImportFixture();

        var view = (await _world.Engine.Neighbors("thing:pane-layout", depth: null, MapWorld.Human, MapWorld.None)).Value;

        Assert.Equal(1, view.Depth);
        Assert.Equal(["thing:pane-layout", "action:pane-resize.resize-focused", "action:split-pane.toggle-orientation", "action:tab-manager.restore-layout"],
            view.Nodes.Select(n => n.Node.Key.Value), StringComparer.Ordinal);
        Assert.Equal(3, view.Edges.Count);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(-1)]
    public async Task A_negative_depth_is_SND_CONTEXT_006(int depth)
    {
        _ = await _world.ImportFixture();

        var view = await _world.Engine.Neighbors("thing:pane-layout", depth, MapWorld.Human, MapWorld.None);

        Assert.Equal(ContextErrors.QueryInvalid.Code, view.Error.Code);
        Assert.Equal(QueryRules.ParseDepth(depth.ToString(System.Globalization.CultureInfo.InvariantCulture)).Error.Message, view.Error.Message);   // one consistent message and fix
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_s_neighbors_stay_inside_its_repo_scope()
    {
        _ = await _world.ImportFixture();

        var view = (await _world.Engine.Neighbors("feature:layout.split-pane", depth: 4, MapWorld.Session("aw-split-panes"), MapWorld.None)).Value;

        Assert.All(view.Nodes, n => Assert.Contains(n.Node.Key.Value, Testing.Map.FixtureMap.ScopedNodes, StringComparer.Ordinal));
    }
}
