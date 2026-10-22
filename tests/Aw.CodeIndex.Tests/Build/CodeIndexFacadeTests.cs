using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Testing;

namespace Aw.CodeIndex.Tests.Build;

public sealed class CodeIndexFacadeTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<BuildHarness> Built()
    {
        var harness = new BuildHarness
        {
            Parser = new FakeSymbolParser()
                .With("src/A.cs", new ParseOutcome([Parsed.Class("A", 1), Parsed.Method("A.Run", 2, Parsed.Call("Stop", 3))]))
                .With("src/B.cs", new ParseOutcome([Parsed.Class("B", 1), Parsed.Method("B.Stop", 2)])),
        };
        _ = harness.Snapshot.WithFile(BuildHarness.First, "src/A.cs", "a").WithFile(BuildHarness.First, "src/B.cs", "b");
        _ = await harness.Build(BuildHarness.First, cancellationToken: Token);
        return harness;
    }

    private static CodeIndexScope OtherScope()
        => new([new CodeIndexRepo(RepoName.From("web"), "/work/web", Core.CodeIndexSettings.Default)], new CodeIndexCaller(CallerKind.Session, RepoName.From("web")));

    private static async Task<SymbolRef> SymbolOf(BuildHarness harness, string name)
        => (await harness.Facade.FindSymbols(harness.Scope, new SymbolQuery(name, SymbolMatch.Exact, Repo: null), Token)).Value.Hits.Single().Ref;

    [Fact, Trait("Lane", "unit")]
    public async Task Status_says_missing_before_a_build_and_reads_the_files_only_after_it()
    {
        var before = await new BuildHarness().Facade.Status(new BuildHarness().Scope, Token);
        Assert.Equal(IndexState.Missing, before.Value.Repos.Single().State);

        var harness = await Built();
        var status = (await harness.Facade.Status(harness.Scope, Token)).Value.Repos.Single();

        Assert.Equal(IndexState.Ok, status.State);
        Assert.Equal(BuildHarness.First, status.Commit);
        Assert.Equal(new IndexCounts(2, 4, 1), status.Counts);
        Assert.Equal(LayerState.Ok, status.Layers.Single(l => l.Layer == LayerName.Structure).State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_says_damaged_for_a_damaged_index_and_heads_has_no_commit_for_it()
    {
        var harness = await Built();
        harness.Store.Damage(BuildHarness.Name);

        var status = (await harness.Facade.Status(harness.Scope, Token)).Value.Repos.Single();
        var heads = (await harness.Facade.Heads(harness.Scope, Token)).Value;

        Assert.Equal(IndexState.Damaged, status.State);
        Assert.Null(heads.Single().Commit);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Heads_names_the_commit_each_index_describes()
    {
        var harness = await Built();

        var heads = (await harness.Facade.Heads(harness.Scope, Token)).Value;

        Assert.Equal(new RecordedHead(BuildHarness.Name, BuildHarness.First), heads.Single());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task FindSymbols_finds_by_name_and_by_prefix_and_stamps_the_answer()
    {
        var harness = await Built();

        var exact = (await harness.Facade.FindSymbols(harness.Scope, new SymbolQuery("Run", SymbolMatch.Exact, Repo: null), Token)).Value;
        var prefix = (await harness.Facade.FindSymbols(harness.Scope, new SymbolQuery("S", SymbolMatch.Prefix, Repo: null), Token)).Value;

        Assert.Equal("Run", exact.Hits.Single().Name);
        Assert.Equal("Stop", prefix.Hits.Single().Name);
        Assert.Equal(BuildHarness.First, exact.Stamp.Commit);
        Assert.Equal(IndexState.Ok, exact.Stamp.State);
        Assert.True(exact.Hits.Single().Untrusted);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task FindSymbols_refuses_an_empty_or_over_limit_query_with_012()
    {
        var harness = await Built();

        var empty = await harness.Facade.FindSymbols(harness.Scope, new SymbolQuery(" ", SymbolMatch.Exact, Repo: null), Token);
        var tooMany = await harness.Facade.FindSymbols(harness.Scope, new SymbolQuery("Run", SymbolMatch.Exact, Repo: null, Limit: 201), Token);

        Assert.Equal("SND-CODEIDX-012", empty.Error.Code);
        Assert.Equal("SND-CODEIDX-012", tooMany.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task FileSymbols_lists_each_files_symbols_and_refuses_over_200_paths()
    {
        var harness = await Built();

        var answer = (await harness.Facade.FileSymbols(harness.Scope, BuildHarness.Name, ["src/A.cs", "src/none.cs"], Token)).Value;
        var tooMany = await harness.Facade.FileSymbols(harness.Scope, BuildHarness.Name, [.. Enumerable.Range(0, 201).Select(i => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"f{i}.cs"))], Token);

        Assert.Equal(2, answer.Files[0].Symbols.Count);
        Assert.Empty(answer.Files[1].Symbols);
        Assert.Equal("SND-CODEIDX-012", tooMany.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Callers_and_callees_follow_the_resolved_edge_with_its_confidence()
    {
        var harness = await Built();
        var run = await SymbolOf(harness, "Run");
        var stop = await SymbolOf(harness, "Stop");

        var callers = (await harness.Facade.Callers(harness.Scope, stop, CallDepth.One, includeImplementations: false, Token)).Value;
        var callees = (await harness.Facade.Callees(harness.Scope, run, CallDepth.One, Token)).Value;

        Assert.Equal(run, callers.Edges.Single().Other!.Ref);
        Assert.Equal(stop, callees.Edges.Single().Other!.Ref);
        Assert.Equal(Coverage.Full, callers.Coverage);
        Assert.Equal(0, callers.UnresolvedCount);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_ref_that_names_no_symbol_is_011_and_a_repo_outside_the_scope_is_the_same_not_built_013()
    {
        var harness = await Built();

        var unknown = await harness.Facade.Callers(harness.Scope, SymbolRef.From("api:src/A.cs#Nope"), CallDepth.One, includeImplementations: false, Token);
        var outside = await harness.Facade.Callers(OtherScope(), await SymbolOf(harness, "Run"), CallDepth.One, includeImplementations: false, Token);

        Assert.Equal("SND-CODEIDX-011", unknown.Error.Code);
        Assert.Equal("SND-CODEIDX-013", outside.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Reads_over_a_repo_with_no_index_answer_013_and_create_nothing()
    {
        var harness = new BuildHarness();

        var found = await harness.Facade.FindSymbols(harness.Scope, new SymbolQuery("Run", SymbolMatch.Exact, Repo: null), Token);

        Assert.Equal("SND-CODEIDX-013", found.Error.Code);
        Assert.Null(await harness.Header());
    }
}
