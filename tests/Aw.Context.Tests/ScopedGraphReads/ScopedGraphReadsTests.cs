using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Context.Core.ScopeGraph;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.ScopedGraphReads;

/// <summary>A scoping Step run's context_* reads, checked against its graph and logged (issue 1005).</summary>
public sealed class ScopedGraphReadsTests : IDisposable
{
    private const string Instance = "ITM-1/line/3/scoping.1";
    private const string Run = "1.1";
    private const string Manager = "feature:main.core";
    private const string Elsewhere = "feature:other.island";   // joined to nothing, so no graph around the manager holds it
    private static readonly string _map = MapWorld.Yaml(
        "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
        $"  - {{key: \"{Manager}\", label: \"Core\"}}",
        "  - {key: \"feature:main.part\", label: \"Part\"}",
        $"  - {{key: \"{Elsewhere}\", label: \"Island\"}}",
        "edges:",
        $"  - {{from: \"feature:main.part\", to: \"{Manager}\", type: part-of, provenance: deterministic}}");
    private readonly MapWorld _world = new();
    private bool _imported;

    public void Dispose() => _world.Dispose();

    private async Task RememberDepthOne()
    {
        if (!_imported)
        {
            _ = await _world.ImportYaml(_map);
            _imported = true;
        }

        var graph = await _world.Engine.ScopeGraph(new ScopeGraphQuery([Manager], MapRead.Current, new LensRange(1, Hops: null), Brief: null, MaxNodes: 400, MaxBytes: 300_000), MapWorld.Human, MapWorld.None);
        Assert.True(graph.IsSuccess, graph.IsError ? graph.Error.Message : null);
        var stored = await _world.Engine.RememberScopeGraph(Instance, graph.Value, MapWorld.None);
        Assert.True(stored.IsSuccess, stored.IsError ? stored.Error.Message : null);
    }

    private ValueTask<Kernel.Result<GraphRead>> Read(string node, string tool = "context_node", bool outside = false, string? reason = null, string run = Run) =>
        _world.Engine.ReadInScopeGraph(Instance, run, new GraphRead(node, tool, outside, reason, _world.Clock.GetUtcNow()), MapWorld.None);

    [Fact, Trait("Lane", "unit")]
    public async Task A_read_of_a_node_in_the_run_graph_is_allowed_and_logged_as_inside()
    {
        await RememberDepthOne();

        var read = await Read(Manager);
        var log = await _world.Engine.GraphReads(Instance, Run, MapWorld.None);

        Assert.True(read.IsSuccess, read.IsError ? read.Error.Message : null);
        var logged = Assert.Single(log.Value);
        Assert.Equal((Manager, "context_node", false, null), (logged.Node, logged.Tool, logged.Outside, logged.Reason));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_read_of_a_node_outside_the_run_graph_answers_not_in_graph_154()
    {
        await RememberDepthOne();

        var read = await Read(Elsewhere, tool: "context_neighbors");

        Assert.True(read.IsError);
        Assert.Equal("SND-CONTEXT-154", read.Error.Code);
        Assert.Contains(Elsewhere, read.Error.Message, StringComparison.Ordinal);
        Assert.Contains("outside", read.Error.Message, StringComparison.Ordinal);   // the fix is in the message
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_read_that_steps_outside_with_a_reason_is_allowed_and_logged_with_outside_and_the_reason()
    {
        await RememberDepthOne();

        var read = await Read(Elsewhere, tool: "context_query", outside: true, reason: "the brief names the pane layout");
        var log = await _world.Engine.GraphReads(Instance, Run, MapWorld.None);

        Assert.True(read.IsSuccess, read.IsError ? read.Error.Message : null);
        var logged = Assert.Single(log.Value);
        Assert.Equal((Elsewhere, "context_query", true, "the brief names the pane layout"), (logged.Node, logged.Tool, logged.Outside, logged.Reason));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task GraphReads_returns_the_reads_of_one_run_in_order_and_none_of_another_run()
    {
        await RememberDepthOne();

        _ = await Read(Manager);
        _ = await Read(Elsewhere, outside: true, reason: "why not");
        _ = await Read(Manager, tool: "context_neighbors", run: "1.2");
        var first = await _world.Engine.GraphReads(Instance, Run, MapWorld.None);
        var second = await _world.Engine.GraphReads(Instance, "1.2", MapWorld.None);
        var none = await _world.Engine.GraphReads(Instance, "9.9", MapWorld.None);

        Assert.Equal([Manager, Elsewhere], first.Value.Select(r => r.Node), StringComparer.Ordinal);
        Assert.Equal(["context_neighbors"], second.Value.Select(r => r.Tool), StringComparer.Ordinal);
        Assert.True(none.IsSuccess);
        Assert.Empty(none.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Remembering_the_graph_again_on_a_later_launch_keeps_the_reads_and_the_graph()
    {
        await RememberDepthOne();
        _ = await Read(Manager);

        await RememberDepthOne();
        var again = await Read(Manager);
        var log = await _world.Engine.GraphReads(Instance, Run, MapWorld.None);

        Assert.True(again.IsSuccess, again.IsError ? again.Error.Message : null);
        Assert.Equal(2, log.Value.Count);
    }
}
