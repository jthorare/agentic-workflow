using System.Globalization;
using Aw.Context.Core.ScopeGraph;
using Aw.Context.Testing.Map;
using Xunit;

namespace Aw.Context.Testing.ScopeGraph;

/// <summary>The scope-run store's contract (issue 1005): the fake and the SQLite store pass the same tests.</summary>
public abstract class ScopeRunStoreContract
{
    private const string Instance = "ITM-1/line/3/scoping.1";
    private static readonly DateTimeOffset _at = new(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken None => TestContext.Current.CancellationToken;

    /// <summary>A store to test, empty.</summary>
    /// <returns>The store.</returns>
    protected abstract IScopeRunStore CreateStore();

    private static ScopeGraphView Graph(params string[] ids) =>
        new(Version: 1, MapVersion: "v1", Anchors: [], Depth: 1, Hops: [], Nodes: [.. ids.Select(Node)], Edges: [], Limits: []);

    private static ScopeGraphNode Node(string id, int index) =>
        new(ScopeGraphNodeId.From(id), Kind: "feature", Path: null, Symbol: null, Summary: id, Rank: index + 1, Lens: "product",
            LensItem: string.Create(CultureInfo.InvariantCulture, $"L-product-{index + 1}"), Band: "own", Citations: []);

    private static GraphRead Inside(string node, string tool, DateTimeOffset at) => new(node, tool, Outside: false, Reason: null, at);

    /// <summary>An instance with no graph holds nothing, not even false.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_instance_with_no_graph_answers_null()
    {
        const string test = nameof(An_instance_with_no_graph_answers_null);

        var held = Expect.Ok(await CreateStore().Holds(Instance, "feature:a", None), test);

        Expect.That(held is null, test, "a graph for an instance that never remembered one");
    }

    /// <summary>A remembered graph holds its nodes and no others, and a newer graph for the instance replaces it.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_remembered_graph_holds_its_nodes_and_the_newest_replaces_the_old()
    {
        const string test = nameof(A_remembered_graph_holds_its_nodes_and_the_newest_replaces_the_old);
        var store = CreateStore();

        _ = Expect.Ok(await store.Remember(Instance, Graph("feature:a", "feature:b"), _at, None), test);
        var first = Expect.Ok(await store.Holds(Instance, "feature:a", None), test);
        var absent = Expect.Ok(await store.Holds(Instance, "feature:z", None), test);
        _ = Expect.Ok(await store.Remember(Instance, Graph("feature:z"), _at, None), test);
        var replaced = Expect.Ok(await store.Holds(Instance, "feature:a", None), test);
        var now = Expect.Ok(await store.Holds(Instance, "feature:z", None), test);

        Expect.That(first == true && absent == false && replaced == false && now == true, test, "the graph doesn't hold exactly its nodes, or the newest didn't replace the old");
    }

    /// <summary>The reads of one run come back in the order they were logged, and no other run's.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Reads_come_back_in_order_per_run()
    {
        const string test = nameof(Reads_come_back_in_order_per_run);
        var store = CreateStore();

        _ = Expect.Ok(await store.Log(Instance, "1.1", Inside("feature:a", "context_node", _at), None), test);
        _ = Expect.Ok(await store.Log(Instance, "1.2", Inside("feature:b", "context_query", _at), None), test);
        _ = Expect.Ok(await store.Log(Instance, "1.1", new GraphRead("feature:z", "context_neighbors", Outside: true, Reason: "why", _at.AddMinutes(1)), None), test);
        var first = Expect.Ok(await store.Reads(Instance, "1.1", None), test);
        var none = Expect.Ok(await store.Reads(Instance, "9.9", None), test);

        Expect.That(first.Select(r => r.Node).SequenceEqual(["feature:a", "feature:z"], StringComparer.Ordinal), test, "the run's reads aren't in logged order");
        Expect.That(first[1].Outside && first[1].Reason == "why", test, "the outside flag or the reason was lost");
        Expect.That(none.Count == 0, test, "reads for a run that made none");
    }

    /// <summary>Remembering the same graph again leaves the graph as it was and the run's reads untouched.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Remembering_the_same_graph_twice_is_idempotent_and_keeps_the_read_log()
    {
        const string test = nameof(Remembering_the_same_graph_twice_is_idempotent_and_keeps_the_read_log);
        var store = CreateStore();
        _ = Expect.Ok(await store.Remember(Instance, Graph("feature:a", "feature:b"), _at, None), test);
        _ = Expect.Ok(await store.Log(Instance, "1.1", Inside("feature:a", "context_node", _at), None), test);

        _ = Expect.Ok(await store.Remember(Instance, Graph("feature:a", "feature:b"), _at.AddMinutes(1), None), test);
        var a = Expect.Ok(await store.Holds(Instance, "feature:a", None), test);
        var b = Expect.Ok(await store.Holds(Instance, "feature:b", None), test);
        var reads = Expect.Ok(await store.Reads(Instance, "1.1", None), test);

        Expect.That(a == true && b == true && reads.Count == 1, test, "the graph changed or the read log didn't survive a second remember");
    }
}
