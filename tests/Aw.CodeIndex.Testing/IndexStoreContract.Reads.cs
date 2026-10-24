using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Xunit;

namespace Aw.CodeIndex.Testing;

public abstract partial class IndexStoreContract
{
    private static SymbolQuery Exact(string text) => new(text, SymbolMatch.Exact, Repo: null);

    private static SymbolQuery Prefix(string text, int limit = CodeIndexRules.DefaultSymbolHits) => new(text, SymbolMatch.Prefix, Repo: null, limit);

    /// <summary>An exact search finds a symbol by name, ref, path or path#name.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_exact_search_matches_name_ref_path_and_path_hash_name()
    {
        var store = Create();
        var run = SymbolOf("src/A.cs", "A.Run");
        _ = await Publish(store, [FileOf("src/A.cs", run, SymbolOf("src/A.cs", "A.Stop", line: 10)), FileOf("src/B.cs", SymbolOf("src/B.cs", "B.Run"))]);

        var reader = await Reader(store);
        await using (reader)
        {
            Expect.That(Refs(Ok(await reader.FindSymbols(Exact("Run"), _none), "by name").Hits).SequenceEqual(["fixture:src/A.cs#A.Run", "fixture:src/B.cs#B.Run"]), "a name matches every symbol with it, ordered by file");
            Expect.That(Refs(Ok(await reader.FindSymbols(Exact(run.Ref.Value), _none), "by ref").Hits).SequenceEqual([run.Ref.Value]), "a ref matches its symbol");
            Expect.That(Refs(Ok(await reader.FindSymbols(Exact("src/A.cs"), _none), "by path").Hits).SequenceEqual(["fixture:src/A.cs#A.Run", "fixture:src/A.cs#A.Stop"]), "a path matches the symbols of its file, in line order");
            Expect.That(Refs(Ok(await reader.FindSymbols(Exact("src/B.cs#Run"), _none), "by path#name").Hits).SequenceEqual(["fixture:src/B.cs#B.Run"]), "path#name matches one symbol");
            Expect.That(Ok(await reader.FindSymbols(Exact("Missing"), _none), "by a missing name").Hits.Count == 0, "a name nobody has matches nothing");
        }
    }

    /// <summary>A prefix search is a range scan on the name and a limit marks the page truncated.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_prefix_search_matches_names_and_truncates_at_its_limit()
    {
        var store = Create();
        _ = await Publish(store, [FileOf("src/A.cs", SymbolOf("src/A.cs", "A.Run", line: 1), SymbolOf("src/A.cs", "A.Runner", line: 8), SymbolOf("src/A.cs", "A.Runeé", line: 16), SymbolOf("src/A.cs", "A.Stop", line: 24), SymbolOf("src/A.cs", "A.Ru", line: 32))]);

        var reader = await Reader(store);
        await using (reader)
        {
            var page = Ok(await reader.FindSymbols(Prefix("Run"), _none), "prefix Run");
            Expect.That(page.Hits.Select(h => h.Name).SequenceEqual(["Run", "Runner", "Runeé"], StringComparer.Ordinal) && !page.Truncated, "every name that starts with the text matches, and no other");
            var cut = Ok(await reader.FindSymbols(Prefix("Run", limit: 2), _none), "prefix Run, limit 2");
            Expect.That(cut.Hits.Count == 2 && cut.Truncated, "a page over its limit is cut and marked truncated");
            Expect.That(Ok(await reader.FindSymbols(Prefix("%"), _none), "prefix %").Hits.Count == 0, "a wildcard character is text, never a pattern");
        }
    }

    /// <summary>An empty text or a limit under 1 is SND-CODEIDX-012.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_query_with_no_text_or_no_room_is_invalid()
    {
        var store = Create();
        _ = await Publish(store, [FileOf("src/A.cs", SymbolOf("src/A.cs", "A.Run"))]);

        var reader = await Reader(store);
        await using (reader)
        {
            Fails(await reader.FindSymbols(Exact(""), _none), "SND-CODEIDX-012", "an empty exact query");
            Fails(await reader.FindSymbols(Prefix(""), _none), "SND-CODEIDX-012", "an empty prefix query");
            Fails(await reader.FindSymbols(Prefix("Run", limit: 0), _none), "SND-CODEIDX-012", "a query with a limit of 0");
        }
    }

    /// <summary>Callers and callees walk to the asked depth, each edge with its level, and unresolved calls are counted.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Walks_follow_edges_to_the_asked_depth_and_count_unresolved_calls()
    {
        var store = Create();
        var a = SymbolOf("src/A.cs", "A.Entry");
        var b = SymbolOf("src/B.cs", "B.Middle");
        var c = SymbolOf("src/C.cs", "C.Leaf");
        _ = await Publish(store, [FileOf("src/A.cs", a), FileOf("src/B.cs", b), FileOf("src/C.cs", c)], CallEdge(a, b, line: 4), CallEdge(b, c, line: 6), UnresolvedEdge(b, "Library", line: 7), UnresolvedEdge(c, "Other", line: 2));

        var reader = await Reader(store);
        await using (reader)
        {
            var one = Ok(await reader.Walk(a.Ref, CallDirection.Callees, CallDepth.One, includeImplementations: false, _none), "callees, depth 1");
            Expect.That(one.Edges.Select(e => (e.Level, e.Name)).SequenceEqual([(1, "Middle")]) && one.UnresolvedCount == 0 && !one.Truncated, "depth 1 lists the direct callees");
            var three = Ok(await reader.Walk(a.Ref, CallDirection.Callees, CallDepth.Three, includeImplementations: false, _none), "callees, depth 3");
            Expect.That(three.Edges.Select(e => (e.Level, e.Name)).SequenceEqual([(1, "Middle"), (2, "Leaf"), (2, "Library"), (3, "Other")]), "deeper levels follow resolved edges, each edge in its level, in stored order");
            Expect.That(three.Edges[0].Other?.Ref == b.Ref && three.Edges[2].Other is null && three.Edges[2].Confidence == Confidence.Unresolved, "an edge names its other end, or none when unresolved");

            var callers = Ok(await reader.Walk(c.Ref, CallDirection.Callers, CallDepth.Two, includeImplementations: false, _none), "callers, depth 2");
            Expect.That(callers.Edges.Select(e => (e.Level, e.Other?.Ref)).SequenceEqual([(1, b.Ref), (2, a.Ref)]), "callers walk the edges backwards");
            var of = Ok(await reader.Walk(b.Ref, CallDirection.Callees, CallDepth.One, includeImplementations: false, _none), "callees of B");
            Expect.That(of.UnresolvedCount == 1, "a symbol's callees count its unresolved calls");
            var into = Ok(await reader.Walk(c.Ref, CallDirection.Callers, CallDepth.One, includeImplementations: false, _none), "callers of C");
            Expect.That(into.UnresolvedCount == 0, "callers count unresolved calls to the symbol's name, which nobody made");
        }
    }

    /// <summary>A walk of an unknown ref is SND-CODEIDX-011; a cycle is walked once.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_walk_of_an_unknown_ref_fails_and_a_cycle_ends()
    {
        var store = Create();
        var a = SymbolOf("src/A.cs", "A.Ping");
        var b = SymbolOf("src/B.cs", "B.Pong");
        _ = await Publish(store, [FileOf("src/A.cs", a), FileOf("src/B.cs", b)], CallEdge(a, b), CallEdge(b, a));

        var reader = await Reader(store);
        await using (reader)
        {
            Fails(await reader.Walk(SymbolOf("src/Z.cs", "Z.Nope").Ref, CallDirection.Callers, CallDepth.One, includeImplementations: false, _none), "SND-CODEIDX-011", "walking an unknown ref");
            var walk = Ok(await reader.Walk(a.Ref, CallDirection.Callees, CallDepth.Three, includeImplementations: false, _none), "walking a cycle");
            Expect.That(walk.Edges.Select(e => (e.Level, e.Name)).SequenceEqual([(1, "Pong"), (2, "Ping")]), "each symbol's edges are followed once");
        }
    }

    /// <summary>A level over 1,000 edges is cut at 1,000 and marked truncated.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_walk_cuts_a_level_at_a_thousand_edges()
    {
        var store = Create();
        var target = SymbolOf("src/T.cs", "T.Target");
        var callers = Enumerable.Range(0, CodeIndexRules.MaxEdgesPerLevel + 1).Select(i => SymbolOf("src/C.cs", $"C.Caller{i.ToString("D3", System.Globalization.CultureInfo.InvariantCulture)}", line: (i * 10) + 1)).ToArray();
        _ = await Publish(store, [FileOf("src/T.cs", target), FileOf("src/C.cs", callers)], [.. callers.Select(c => CallEdge(c, target))]);

        var reader = await Reader(store);
        await using (reader)
        {
            var walk = Ok(await reader.Walk(target.Ref, CallDirection.Callers, CallDepth.One, includeImplementations: false, _none), "callers");
            Expect.That(walk.Edges.Count == CodeIndexRules.MaxEdgesPerLevel && walk.Truncated, "the level is cut at the limit and marked");
        }
    }

    /// <summary>Callers of an interface member also list the callers of its implementations, when asked; inherits edges are not calls.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Callers_can_include_the_callers_of_implementations()
    {
        var store = Create();
        var contract = SymbolOf("src/I.cs", "Svc.IRunner", SymbolKind.Interface);
        var contractRun = SymbolOf("src/I.cs", "Svc.IRunner.Run", arity: 1, line: 3);
        var impl = SymbolOf("src/Impl.cs", "Svc.Impl", SymbolKind.Class);
        var implRun = SymbolOf("src/Impl.cs", "Svc.Impl.Run", arity: 1, line: 3);
        var other = SymbolOf("src/Impl.cs", "Svc.Impl.Run", arity: 2, line: 9) with { Ref = SymbolRef.From("fixture:src/Impl.cs#Svc.Impl.Run~2") };
        var viaContract = SymbolOf("src/C.cs", "Svc.Caller.ViaContract");
        var viaImpl = SymbolOf("src/C.cs", "Svc.Caller.ViaImpl", line: 9);
        var inherits = new EdgeRecord(impl.Ref, contract.Ref, "IRunner", EdgeKind.Inherits, EdgeBasis.Syntax, Confidence.Certain, Line: 1, Candidates: 1);
        _ = await Publish(store, [FileOf("src/I.cs", contract, contractRun), FileOf("src/Impl.cs", impl, implRun, other), FileOf("src/C.cs", viaContract, viaImpl)], inherits, CallEdge(viaContract, contractRun), CallEdge(viaImpl, implRun));

        var reader = await Reader(store);
        await using (reader)
        {
            var plain = Ok(await reader.Walk(contractRun.Ref, CallDirection.Callers, CallDepth.One, includeImplementations: false, _none), "callers");
            Expect.That(plain.Edges.Select(e => e.Other?.Ref).SequenceEqual([viaContract.Ref]), "without the flag only direct callers are listed");
            var all = Ok(await reader.Walk(contractRun.Ref, CallDirection.Callers, CallDepth.One, includeImplementations: true, _none), "callers with implementations");
            Expect.That(Refs(all.Edges.Select(e => e.Other!)).Order(StringComparer.Ordinal).SequenceEqual(new[] { viaContract.Ref.Value, viaImpl.Ref.Value }.Order(StringComparer.Ordinal), StringComparer.Ordinal), "with the flag the callers of the implementing member of the same name and arity join");
            var downward = Ok(await reader.Walk(implRun.Ref, CallDirection.Callers, CallDepth.One, includeImplementations: true, _none), "callers of the implementation");
            Expect.That(downward.Edges.Select(e => e.Other?.Ref).SequenceEqual([viaImpl.Ref]), "an implementation has no implementations of its own");
            var callees = Ok(await reader.Walk(viaContract.Ref, CallDirection.Callees, CallDepth.Two, includeImplementations: true, _none), "callees");
            Expect.That(callees.Edges.Count == 1, "inherits edges are never followed as calls");
        }
    }
}
