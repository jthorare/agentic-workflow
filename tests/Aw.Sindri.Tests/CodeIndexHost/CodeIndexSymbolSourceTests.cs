using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Testing;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Symbols;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Host.CodeIndexHost;
using IndexHit = Aw.CodeIndex.Core.Facade.SymbolHit;
using SymbolQuery = Aw.Context.Core.Symbols.SymbolQuery;

namespace Aw.Sindri.Tests.CodeIndexHost;

/// <summary>The adapter behind Context's code symbol port (S-9): the block's answers become heads and rows (code-index spec §7.2, §7.3), over a <see cref="FakeCodeIndex"/>.</summary>
[Lane(TestLane.Unit)]
public sealed class CodeIndexSymbolSourceTests
{
    private const string Indexed = "0123456789abcdef0123456789abcdef01234567";
    private const string Other = "1234567890abcdef1234567890abcdef12345678";
    private static readonly RepoName _name = RepoName.From("demo");
    private static readonly ContextRepoName _repo = ContextRepoName.From("demo");
    private static readonly SymbolRef _run = SymbolRef.From("demo:src/Worker.cs#Worker.Run");
    private static readonly SymbolRef _stop = SymbolRef.From("demo:src/Worker.cs#Worker.Stop");
    private static readonly SymbolRef _helper = SymbolRef.From("demo:src/Helper.cs#Helper.Assist");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_index_at_the_maps_commit_is_ok_and_its_rows_carry_the_commit()
    {
        var source = Source(Index());

        var head = (await source.Heads(_repo, Indexed, Ct)).Value;
        var callers = (await source.Callers(_repo, "src/Worker.cs#Worker.Run", 1, Indexed, Ct)).Value;

        Assert.Equal((CodeIndexState.Ok, Indexed, null), (head.State, head.Commit, head.AsOfCommit));
        var row = Assert.Single(callers.Rows);
        Assert.Equal(("src/Worker.cs#Worker.Stop", "Worker.Stop", "method", "syntax", 1.0, "full", Indexed, null, SymbolsState.Ok), (row.Symbol.Ref, row.Symbol.Name, row.Symbol.Kind, row.Basis, row.Confidence, row.Coverage, row.Commit, row.AsOfCommit, row.State));
    }

    [Fact]
    public async Task An_index_at_another_commit_than_the_maps_is_stale_with_both_commits_on_every_row()
    {
        var source = Source(Index());

        var head = (await source.Heads(_repo, Other, Ct)).Value;
        var files = (await source.FileSymbols(_repo, ["src/Worker.cs"], Other, Ct)).Value;
        var found = (await source.FindSymbols(_repo, new SymbolQuery("Worker.", Prefix: true, Max: 10), Other, Ct)).Value;
        var callees = (await source.Callees(_repo, "src/Worker.cs#Worker.Run", 1, Other, Ct)).Value;

        Assert.Equal((CodeIndexState.Stale, Indexed, Other), (head.State, head.Commit, head.AsOfCommit));
        Assert.Equal(2, found.Rows.Count);
        Assert.All(found.Rows, r => Assert.Equal((Indexed, Other, SymbolsState.Stale), (r.Commit, r.AsOfCommit, r.State)));
        Assert.All(files.Rows, r => Assert.Equal((Indexed, Other, SymbolsState.Stale), (r.Commit, r.AsOfCommit, r.State)));
        Assert.All(callees.Rows, r => Assert.Equal((Indexed, Other, SymbolsState.Stale), (r.Commit, r.AsOfCommit, r.State)));
    }

    [Fact]
    public async Task The_same_commit_abbreviated_is_not_a_mismatch()
    {
        var head = (await Source(Index()).Heads(_repo, Indexed[..9], Ct)).Value;

        Assert.Equal((CodeIndexState.Ok, null), (head.State, head.AsOfCommit));
    }

    [Fact]
    public async Task A_file_read_gives_a_row_per_known_file_and_none_for_an_unknown_one()
    {
        var files = (await Source(Index()).FileSymbols(_repo, ["src/Worker.cs", "src/none.cs"], Indexed, Ct)).Value;

        var row = Assert.Single(files.Rows);
        Assert.Equal("src/Worker.cs: src/Worker.cs#Worker.Run, src/Worker.cs#Worker.Stop", $"{row.Path}: {string.Join(", ", row.Symbols.Select(static s => s.Ref))}");
    }

    [Fact]
    public async Task A_call_site_that_resolved_to_nothing_counts_as_unresolved_and_is_not_a_row()
    {
        var callees = (await Source(Index()).Callees(_repo, "src/Worker.cs#Worker.Run", 1, Indexed, Ct)).Value;

        Assert.Equal("src/Helper.cs#Helper.Assist", Assert.Single(callees.Rows).Symbol.Ref);
        Assert.Equal(3, callees.Unresolved);
        Assert.Equal(0.75, callees.Rows[0].Confidence);
    }

    [Fact]
    public async Task A_repo_with_no_index_is_missing_with_no_rows_for_every_call()
    {
        var source = Source(new FakeCodeIndex());

        var head = (await source.Heads(_repo, Indexed, Ct)).Value;
        var files = (await source.FileSymbols(_repo, ["src/Worker.cs"], Indexed, Ct)).Value;
        var callers = (await source.Callers(_repo, "src/Worker.cs#Worker.Run", 1, Indexed, Ct)).Value;

        Assert.Equal((CodeIndexState.Missing, null, "SND-CODEIDX-013"), (head.State, head.Commit, head.Code));
        Assert.Empty(files.Rows);
        Assert.Empty(callers.Rows);
    }

    [Fact]
    public async Task A_damaged_index_answers_damaged_with_the_010_code_and_no_rows()
    {
        var source = Source(new FakeCodeIndex().WithDamaged(_name));

        var files = (await source.FileSymbols(_repo, ["src/Worker.cs"], Indexed, Ct)).Value;

        Assert.Equal((CodeIndexState.Damaged, "SND-CODEIDX-010", 0), (files.Head.State, files.Head.Code, files.Rows.Count));
    }

    [Fact]
    public async Task A_graph_layer_that_is_down_is_named_on_the_head_and_the_structure_still_answers()
    {
        var source = Source(Index().WithGraphUnavailable(_name));

        var callers = (await source.Callers(_repo, "src/Worker.cs#Worker.Run", 1, Indexed, Ct)).Value;

        Assert.Equal((CodeIndexState.LayerUnavailable, "graph", "SND-CODEIDX-008", 1), (callers.Head.State, callers.Head.Layer, callers.Head.Code, callers.Rows.Count));
    }

    [Fact]
    public async Task A_repo_outside_the_scope_is_not_configured_and_the_index_is_never_asked_for_rows()
    {
        var source = Source(Index(), scopeRepo: "elsewhere");

        var head = (await source.Heads(_repo, Indexed, Ct)).Value;
        var files = (await source.FileSymbols(_repo, ["src/Worker.cs"], Indexed, Ct)).Value;

        Assert.Equal(CodeIndexState.NotConfigured, head.State);
        Assert.Empty(files.Rows);
    }

    [Fact]
    public async Task A_ref_the_index_does_not_know_has_no_edges()
    {
        var callers = (await Source(Index()).Callers(_repo, "src/Worker.cs#Worker.Nope", 1, Indexed, Ct)).Value;

        Assert.Equal((CodeIndexState.Ok, 0), (callers.Head.State, callers.Rows.Count));
    }

    [Fact]
    public async Task A_name_search_finds_symbols_by_prefix_with_their_sites()
    {
        var found = (await Source(Index()).FindSymbols(_repo, new SymbolQuery("Worker.R", Prefix: true, Max: 10), Indexed, Ct)).Value;

        var hit = Assert.Single(found.Rows);
        Assert.Equal(("src/Worker.cs#Worker.Run", "Worker.Run", 10, 20, true), (hit.Site.Ref, hit.Site.Name, hit.Site.StartLine, hit.Site.EndLine, hit.Site.Exported));
    }

    private static CodeIndexSymbolSource Source(FakeCodeIndex index, string scopeRepo = "demo") => new(index, new FixedScope(scopeRepo));

    private static FakeCodeIndex Index()
    {
        var run = Hit(_run, "src/Worker.cs", "Worker.Run", 10, 20);
        var stop = Hit(_stop, "src/Worker.cs", "Worker.Stop", 22, 30);
        var helper = Hit(_helper, "src/Helper.cs", "Helper.Assist", 5, 9);
        return new FakeCodeIndex()
            .WithIndex(_name, CommitSha.From(Indexed), run, stop, helper)
            .WithEdges(_name, _run, CallDirection.Callers, unresolved: 0, new CallEdge(1, stop, "Run", EdgeKind.Calls, EdgeBasis.Syntax, Confidence.Certain, Line: 25, Candidates: 1, Coverage.Full))
            .WithEdges(_name, _run, CallDirection.Callees, unresolved: 2,
                new CallEdge(1, helper, "Assist", EdgeKind.Calls, EdgeBasis.Syntax, Confidence.Likely, Line: 12, Candidates: 1, Coverage.Full),
                new CallEdge(1, Other: null, "Log", EdgeKind.Calls, EdgeBasis.Graphify, Confidence.Likely, Line: 14, Candidates: 0, Coverage.Partial));
    }

    private static IndexHit Hit(SymbolRef symbol, string file, string name, int start, int end) => new(symbol, SymbolKind.Method, name, file, start, end, "void " + name + "()", Exported: true);

    private sealed class FixedScope(string repo) : ICodeIndexScopeSource
    {
        public ValueTask<Result<CodeIndexScope>> Current(CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<CodeIndexScope>>(new CodeIndexScope([new CodeIndexRepo(RepoName.From(repo), "/tmp/" + repo, CodeIndexSettings.Create(new CodeIndexSettingsInput(DenyPaths: [])).Value)], new CodeIndexCaller(CallerKind.Human, Repo: null)));
    }
}
