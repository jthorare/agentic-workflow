using System.Globalization;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Resolution;
using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Tests.Resolution;

public sealed class CallResolverTests
{
    private static ResolutionSymbol Sym(string file, string qualified, SymbolKind kind = SymbolKind.Method, int arity = 0, int line = 1) =>
        new(SymbolRef.From(string.Create(CultureInfo.InvariantCulture, $"r:{file}#{qualified}@{line}")), file, qualified[(qualified.LastIndexOf('.') + 1)..], qualified, kind, arity, line, line);

    private static ResolutionSymbol TypeSym(string file, string qualified, int line = 1) => Sym(file, qualified, SymbolKind.Class, 0, line);

    private static CallSiteRow Call(ResolutionSymbol caller, string name, int arity = 0, string receiver = "other", int line = 10, EdgeKind kind = EdgeKind.Calls) =>
        new(caller.Ref, new CallSite(kind, name, arity, receiver, line));

    private static IReadOnlyList<EdgeRecord> Resolve(IEnumerable<ResolutionSymbol> symbols, params CallSiteRow[] rows) =>
        new CallResolver([.. symbols], [""]).Resolve(rows);

    [Fact, Trait("Lane", "unit")]
    public void One_candidate_on_this_is_certain()
    {
        var caller = Sym("a/A.cs", "N.A.Run");
        var callee = Sym("a/A.cs", "N.A.Go", line: 5);

        var edge = Assert.Single(Resolve([TypeSym("a/A.cs", "N.A"), caller, callee], Call(caller, "Go", receiver: "this")));

        Assert.Equal(Confidence.Certain, edge.Confidence);
        Assert.Equal(callee.Ref, edge.Target);
        Assert.Equal(1, edge.Candidates);
        Assert.Equal(EdgeBasis.Syntax, edge.Basis);
    }

    [Fact, Trait("Lane", "unit")]
    public void One_candidate_on_another_receiver_is_likely()
    {
        var caller = Sym("a/A.cs", "N.A.Run");
        var callee = Sym("b/B.cs", "N.B.Go");

        var edge = Assert.Single(Resolve([caller, callee], Call(caller, "Go")));

        Assert.Equal(Confidence.Likely, edge.Confidence);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_type_name_that_matches_the_candidates_container_is_certain_and_one_that_does_not_is_likely()
    {
        var caller = Sym("a/A.cs", "N.A.Run");
        var callee = Sym("b/B.cs", "N.B.Go");

        Assert.Equal(Confidence.Certain, Assert.Single(Resolve([caller, callee], Call(caller, "Go", receiver: "type:B"))).Confidence);
        Assert.Equal(Confidence.Likely, Assert.Single(Resolve([caller, callee], Call(caller, "Go", receiver: "type:Other"))).Confidence);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_candidate_is_unresolved_with_no_target_and_the_name_kept()
    {
        var caller = Sym("a/A.cs", "N.A.Run");

        var edge = Assert.Single(Resolve([caller], Call(caller, "WriteLine", receiver: "type:Console")));

        Assert.Equal(Confidence.Unresolved, edge.Confidence);
        Assert.Null(edge.Target);
        Assert.Equal("WriteLine", edge.TargetName);
        Assert.Equal(0, edge.Candidates);
    }

    [Fact, Trait("Lane", "unit")]
    public void Only_the_arity_of_the_call_matches_and_minus_one_matches_any()
    {
        var caller = Sym("a/A.cs", "N.A.Run");
        var one = Sym("a/A.cs", "N.A.Go", arity: 1, line: 5);
        var two = Sym("a/A.cs", "N.A.Go", arity: 2, line: 6);

        var exact = Assert.Single(Resolve([caller, one, two], Call(caller, "Go", arity: 2, receiver: "this")));
        var group = Resolve([caller, one, two], Call(caller, "Go", arity: -1, receiver: "this"));

        Assert.Equal(two.Ref, exact.Target);
        Assert.Equal(2, group.Count);
        Assert.All(group, e => Assert.Equal(Confidence.Ambiguous, e.Confidence));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_call_matches_callables_only_and_an_inheritance_matches_types_only()
    {
        var caller = TypeSym("a/A.cs", "N.A");
        var type = TypeSym("a/B.cs", "N.B");
        var method = Sym("a/C.cs", "N.C.B");

        var call = Assert.Single(Resolve([caller, type, method], Call(caller, "B", receiver: "other")));
        var inherits = Assert.Single(Resolve([caller, type, method], Call(caller, "B", receiver: "type:B", kind: EdgeKind.Inherits)));

        Assert.Equal(method.Ref, call.Target);
        Assert.Equal(type.Ref, inherits.Target);
        Assert.Equal(Confidence.Certain, inherits.Confidence);
    }

    [Fact, Trait("Lane", "unit")]
    public void Several_candidates_give_one_ambiguous_edge_each_ranked_by_file_then_namespace_then_project_directory_then_path()
    {
        var caller = Sym("p/q/A.cs", "N.A.Run");
        var sameFile = Sym("p/q/A.cs", "N.A.Go", line: 20);
        var sameNamespace = Sym("p/z/Z.cs", "N.Z.Go");                  // namespace N, other project directory
        var sameProject = Sym("p/q/B.cs", "M.B.Go");                     // other namespace, same project directory
        var farther = Sym("a/Other.cs", "M.O.Go");
        var symbols = new[] { TypeSym("p/q/A.cs", "N.A"), TypeSym("p/z/Z.cs", "N.Z"), TypeSym("p/q/B.cs", "M.B"), TypeSym("a/Other.cs", "M.O"), caller, farther, sameProject, sameNamespace, sameFile };

        var edges = new CallResolver(symbols, ["p/q", "p/z", "a", ""]).Resolve([Call(caller, "Go")]);

        Assert.Equal([sameFile.Ref, sameNamespace.Ref, sameProject.Ref, farther.Ref], edges.Select(e => e.Target!.Value));
        Assert.All(edges, e => Assert.Equal(4, e.Candidates));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_namespace_of_a_nested_type_stops_before_the_outermost_type()
    {
        var caller = Sym("a/A.cs", "N.Outer.Inner.Run");
        var inOuter = Sym("b/B.cs", "N.Other.Go");                      // namespace N
        var inInner = Sym("c/C.cs", "N.Outer.Inner.Go");                 // the same outer type declared in another file: namespace N too
        var elsewhere = Sym("0/Z.cs", "M.Z.Go");
        var symbols = new[] { TypeSym("a/A.cs", "N.Outer"), TypeSym("a/A.cs", "N.Outer.Inner", 2), caller, TypeSym("b/B.cs", "N.Other"), inOuter, TypeSym("c/C.cs", "N.Outer"), TypeSym("c/C.cs", "N.Outer.Inner", 2), inInner, TypeSym("0/Z.cs", "M.Z"), elsewhere };

        var edges = new CallResolver(symbols, [""]).Resolve([Call(caller, "Go")]);

        Assert.Equal([inOuter.Ref, inInner.Ref, elsewhere.Ref], edges.Select(e => e.Target!.Value));   // N before M, then path order (b/ before c/)
    }

    [Fact, Trait("Lane", "unit")]
    public void An_ambiguous_call_keeps_sixty_four_edges_and_the_true_count()
    {
        var caller = Sym("a/A.cs", "N.A.Run");
        var handlers = Enumerable.Range(0, 300).Select(i => Sym(string.Create(CultureInfo.InvariantCulture, $"h/{i:D3}.cs"), string.Create(CultureInfo.InvariantCulture, $"N.H{i:D3}.Handle"))).ToArray();

        var edges = Resolve([caller, .. handlers.Reverse()], Call(caller, "Handle"));

        Assert.Equal(CodeIndexRules.MaxAmbiguousEdges, edges.Count);
        Assert.All(edges, e => Assert.Equal(300, e.Candidates));
        Assert.Equal(handlers.Take(CodeIndexRules.MaxAmbiguousEdges).Select(h => h.Ref), edges.Select(e => e.Target!.Value));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(1, 300, "1 of 300")]
    [InlineData(8, 300, "8 of 300")]
    [InlineData(2, 2, "2 of 2")]
    public void The_rank_reads_k_of_N(int rank, int candidates, string expected) => Assert.Equal(expected, CallResolver.RankText(rank, candidates));

    [Fact, Trait("Lane", "unit")]
    public void Two_calls_with_the_same_caller_name_kind_line_and_target_are_one_edge_and_the_first_wins()
    {
        var caller = Sym("a/A.cs", "N.A.Run");
        var callee = Sym("a/A.cs", "N.A.Go", line: 5);

        var edges = Resolve([caller, callee], Call(caller, "Go", receiver: "this", line: 14), Call(caller, "Go", receiver: "other", line: 14), Call(caller, "Go", receiver: "other", line: 15));

        Assert.Equal([(14, Confidence.Certain), (15, Confidence.Likely)], edges.Select(e => (e.Line, e.Confidence)));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_call_site_whose_caller_is_not_a_known_symbol_is_left_out()
    {
        var known = Sym("a/A.cs", "N.A.Run");
        var stranger = Sym("z/Z.cs", "N.Z.Run");

        Assert.Empty(Resolve([known], Call(stranger, "Go")));
    }
}
