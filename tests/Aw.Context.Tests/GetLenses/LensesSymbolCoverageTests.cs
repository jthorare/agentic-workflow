using Aw.Context.Core.Engine;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Context.Core.Symbols;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Map;
using Aw.Context.Testing.Symbols;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.GetLenses;

/// <summary>Phase 9: the lenses' 'coverage.code.level' is 'symbol' when the code index answers for the code band's files, else it stays 'file'.</summary>
public sealed class LensesSymbolCoverageTests
{
    private const string Anchor = "feature:tabs.tab-manager";
    private static readonly ContextRepoName _repo = ContextRepoName.From(FixtureMap.Repo);

    private static readonly Dictionary<string, string> _tree = new(StringComparer.Ordinal)
    {
        ["src/tabs/TabManager.ts"] = "export class TabManager {}",
        ["src/tabs/TabGroups.ts"] = "export const b = 1;",
    };

    private static readonly SymbolSite _manager = new("src/tabs/TabManager.ts#TabManager", "TabManager", "class", "src/tabs/TabManager.ts", 1, 9, Signature: null, Exported: true);

    private const string TwoRepos = """{"repos":["aw-split-panes","other"]}""";

    private static async Task<LensesView> Ask(FakeIndexedRepo? index, string section = MapWorld.OnlyFixtureRepo)
    {
        using var world = new MapWorld(new FakeRepoReader(MapWorld.Repos[0], MapWorld.Head, _tree), symbols: new FakeCodeSymbolSource(index is null ? [] : [index]));
        _ = await world.ImportYaml(string.Concat("{\"commit\": \"", MapWorld.Head, "\", ", MapWorld.TabsMap.TrimStart().AsSpan(1)));
        return (await world.Engine.Lenses(Anchor, MapRead.Current, depth: null, MapWorld.HumanWith(section), MapWorld.None)).Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_index_that_answers_makes_the_code_coverage_symbol_level()
    {
        var view = await Ask(new FakeIndexedRepo(_repo, CodeIndexState.Ok, MapWorld.Head, [_manager], []));

        Assert.Equal((SymbolsState.Ok, "symbol"), (view.Bands.Code.Symbols.State, view.Coverage.Code.Level));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stale_index_still_answers_so_the_level_is_symbol()
    {
        var view = await Ask(new FakeIndexedRepo(_repo, CodeIndexState.Ok, "7c3e1a2f4b5d6e7081920a1b2c3d4e5f60718293", [_manager], []));

        Assert.Equal((SymbolsState.Stale, "symbol"), (view.Bands.Code.Symbols.State, view.Coverage.Code.Level));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_index_keeps_the_level_at_file()
    {
        var view = await Ask(index: null);

        Assert.Equal((SymbolsState.Unavailable, "file"), (view.Bands.Code.Symbols.State, view.Coverage.Code.Level));
    }

    // #3776: a profile with two repos in scope read no symbols and said "index not built" while the code files' repo had an index.
    [Fact, Trait("Lane", "unit")]
    public async Task Two_repos_in_scope_still_read_the_symbols_of_the_repo_the_files_are_in()
    {
        var view = await Ask(new FakeIndexedRepo(_repo, CodeIndexState.Ok, MapWorld.Head, [_manager], []), TwoRepos);

        Assert.Equal((SymbolsState.Ok, "symbol", null), (view.Bands.Code.Symbols.State, view.Coverage.Code.Level, view.Bands.Code.Symbols.Reason));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Two_repos_in_scope_report_a_stale_index_as_stale()
    {
        var view = await Ask(new FakeIndexedRepo(_repo, CodeIndexState.Ok, "7c3e1a2f4b5d6e7081920a1b2c3d4e5f60718293", [_manager], []), TwoRepos);

        Assert.Equal((SymbolsState.Stale, "symbol"), (view.Bands.Code.Symbols.State, view.Coverage.Code.Level));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Two_repos_in_scope_with_no_index_say_index_not_built()
    {
        var view = await Ask(index: null, TwoRepos);

        Assert.Equal((SymbolsState.Unavailable, "index not built", "file"), (view.Bands.Code.Symbols.State, view.Bands.Code.Symbols.Reason, view.Coverage.Code.Level));
    }

    // The choice for files in 2 repos: the band's state is the worst repo's, with that repo's own reason, and the repos that answered keep their symbols.
    [Fact, Trait("Lane", "unit")]
    public void Files_in_two_repos_join_to_the_worst_state_and_keep_the_answering_repos_files()
    {
        var ok = new LensSymbols(SymbolsState.Ok, null, []) { Files = [new FileSymbolsRow("a.ts", [], "aaa", null, SymbolsState.Ok)], Commit = "aaa" };
        var down = LensSymbols.Unavailable with { Reason = "index damaged" };

        var joined = Features.GetLenses.RepoSymbols.Join([ok, down]);

        Assert.Equal((SymbolsState.Unavailable, "index damaged", 1, null), (joined.State, joined.Reason, joined.Files!.Count, joined.Commit));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_ok_and_a_stale_repo_join_to_stale()
    {
        var ok = new LensSymbols(SymbolsState.Ok, null, []) { Files = [] };
        var stale = new LensSymbols(SymbolsState.Stale, "stale", []) { Files = [], AsOfMismatch = true };

        var joined = Features.GetLenses.RepoSymbols.Join([ok, stale]);

        Assert.Equal((SymbolsState.Stale, "stale", true), (joined.State, joined.Reason, joined.AsOfMismatch));
    }
}
