using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Results;
using Aw.Context.Core.Symbols;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Symbols;

namespace Aw.Context.Tests.Symbols;

/// <summary>An index behind the map along main is catching up (#990): the pipeline decides it with the repo's history and restamps the rows by file; every other case stays as the index source reported it.</summary>
public sealed class TrailingCodeSymbolSourceTests
{
    private const string Indexed = CodeSymbolSourceContract.Indexed;
    private const string Other = CodeSymbolSourceContract.Other;
    private const string Diverged = "fedcba9876543210fedcba9876543210fedcba98";
    private static readonly ContextRepoName _repo = CodeSymbolSourceContract.Repo;
    private static readonly ContextRepo _checkout = new(_repo, "/repos/split-panes");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task An_ancestor_index_is_catching_up_with_the_changed_files_pending_and_the_denied_ones_left_out()
    {
        var source = Trailing(Index(), Git(Indexed, "src/tabs/Manager.ts", "src/layout/SplitPane.ts", ".env"));

        var head = (await source.Heads(_repo, Other, Ct)).Value;

        Assert.Equal((CodeIndexState.CatchingUp, Indexed, Other), (head.State, head.Commit, head.AsOfCommit));
        Assert.Equal(["src/layout/SplitPane.ts", "src/tabs/Manager.ts"], head.Pending, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Rows_are_stale_in_pending_files_and_ok_in_the_others()
    {
        var source = Trailing(Index(), Git(Indexed, "src/layout/SplitPane.ts"));

        var found = (await source.FindSymbols(_repo, new SymbolQuery("", Prefix: true, Max: 10), Other, Ct)).Value;
        var files = (await source.FileSymbols(_repo, ["src/layout/SplitPane.ts", "src/layout/paneStore.ts"], Other, Ct)).Value;
        var callers = (await source.Callers(_repo, "src/layout/SplitPane.ts#resize", 1, Other, Ct)).Value;

        Assert.Equal(CodeIndexState.CatchingUp, found.Head.State);
        Assert.All(found.Rows, r => Assert.Equal(r.Site.Path == "src/layout/SplitPane.ts" ? (SymbolsState.Stale, Other) : (SymbolsState.Ok, null), (r.State, r.AsOfCommit)));
        Assert.Equal((SymbolsState.Stale, Other), (files.Rows.Single(r => r.Path == "src/layout/SplitPane.ts").State, files.Rows.Single(r => r.Path == "src/layout/SplitPane.ts").AsOfCommit));
        Assert.Equal((SymbolsState.Ok, null), (files.Rows.Single(r => r.Path == "src/layout/paneStore.ts").State, files.Rows.Single(r => r.Path == "src/layout/paneStore.ts").AsOfCommit));
        Assert.Equal(SymbolsState.Stale, Assert.Single(callers.Rows).State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_index_that_is_not_an_ancestor_stays_stale()
    {
        var source = Trailing(Index(), Git(Diverged, "src/layout/SplitPane.ts"));

        var head = (await source.Heads(_repo, Other, Ct)).Value;
        var files = (await source.FileSymbols(_repo, ["src/layout/paneStore.ts"], Other, Ct)).Value;

        Assert.Equal((CodeIndexState.Stale, null), (head.State, head.Pending));
        Assert.All(files.Rows, r => Assert.Equal(SymbolsState.Stale, r.State));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_index_whose_ancestry_git_cannot_tell_stays_stale()
    {
        var head = (await Trailing(Index(), Git(mergeBase: null)).Heads(_repo, Other, Ct)).Value;

        Assert.Equal((CodeIndexState.Stale, null), (head.State, head.Pending));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_graph_layer_that_is_down_stays_degraded_even_for_an_ancestor()
    {
        var down = Index() with { State = CodeIndexState.LayerUnavailable, Layer = "graph", Code = "SND-CODEIDX-020" };

        var head = (await Trailing(down, Git(Indexed, "src/layout/SplitPane.ts")).Heads(_repo, Other, Ct)).Value;

        Assert.Equal((CodeIndexState.Stale, null, "graph"), (head.State, head.Pending, head.Layer));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_index_at_the_maps_commit_is_not_asked_of_git()
    {
        var head = (await Trailing(Index(), Git(mergeBase: null)).Heads(_repo, Indexed, Ct)).Value;

        Assert.Equal((CodeIndexState.Ok, null), (head.State, head.Pending));
    }

    private static FakeIndexedRepo Index() => new(_repo, CodeIndexState.Ok, Indexed, CodeSymbolSourceContract.Symbols, CodeSymbolSourceContract.Calls);

    private static TrailingCodeSymbolSource Trailing(FakeIndexedRepo index, FakeRepoReader git) => new(new FakeCodeSymbolSource([index]), git, _checkout);

    // A checkout that holds both commits; 'mergeBase' is what git would say the merge base of the index's commit and the map's is (null: no answer).
    private static FakeRepoReader Git(string? mergeBase = Indexed, params string[] changed) =>
        new FakeRepoReader(_checkout, Other, new Dictionary<string, string>(StringComparer.Ordinal))
            .WithHistory([Indexed, Other, Diverged], mergeBase is null ? [] : [new RepoChanges(Indexed, Other, mergeBase, [.. changed.Select(p => new RepoChange(p, ChangeKind.Modified))])]);
}
