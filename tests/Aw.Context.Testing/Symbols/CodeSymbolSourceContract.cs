using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Symbols;
using Aw.Context.Testing.Map;
using Xunit;

namespace Aw.Context.Testing.Symbols;

/// <summary>
/// The code symbol source's contract (code index spec §7.2, §7.3): the fake and Sindri's adapter over the real index pass the same tests. Every source is seeded from <see cref="FakeIndexedRepo"/> rows, so
/// a stale index, an index that is ahead of the map and a repo with no index are cases of the contract, not of one implementation.
/// </summary>
public abstract class CodeSymbolSourceContract
{
    /// <summary>The repo every seed names.</summary>
    public static ContextRepoName Repo { get; } = ContextRepoName.From("split-panes");

    /// <summary>The commit the seeded index describes.</summary>
    public const string Indexed = "7c3e1a2f4b5d6e7081920a1b2c3d4e5f60718293";

    /// <summary>A commit that is not the indexed one (the map's, when the map is behind or ahead of the index).</summary>
    public const string Other = "1234567890abcdef1234567890abcdef12345678";

    /// <summary>The symbols the seeded index holds.</summary>
    public static IReadOnlyList<SymbolSite> Symbols { get; } =
    [
        new("src/layout/SplitPane.ts#setOrientation", "setOrientation", "method", "src/layout/SplitPane.ts", 12, 30, "setOrientation(o: Orientation): void", Exported: true),
        new("src/layout/SplitPane.ts#resize", "resize", "method", "src/layout/SplitPane.ts", 32, 60, "resize(px: number): void", Exported: true),
        new("src/layout/paneStore.ts#save", "save", "function", "src/layout/paneStore.ts", 5, 20, Signature: null, Exported: false),
        new("src/tabs/Manager.ts#restoreLayout", "restoreLayout", "method", "src/tabs/Manager.ts", 40, 70, "restoreLayout(): Promise<void>", Exported: true),
    ];

    /// <summary>The call edges of the seeded index: restoreLayout calls setOrientation, which calls resize, which calls save.</summary>
    public static IReadOnlyList<FakeCall> Calls { get; } =
    [
        new("src/tabs/Manager.ts#restoreLayout", "src/layout/SplitPane.ts#setOrientation", "semantic", 1.0, "full"),
        new("src/layout/SplitPane.ts#setOrientation", "src/layout/SplitPane.ts#resize", "semantic", 0.9, "full"),
        new("src/layout/SplitPane.ts#resize", "src/layout/paneStore.ts#save", "syntax", 0.6, "partial"),
    ];

    private static CancellationToken None => TestContext.Current.CancellationToken;

    /// <summary>Makes the source over the repos.</summary>
    /// <param name="repos">What the index holds; a repo not listed has no index.</param>
    /// <returns>The source.</returns>
    protected abstract ICodeSymbolSource Create(IReadOnlyList<FakeIndexedRepo> repos);

    /// <summary>The index built at <see cref="Indexed"/>, healthy.</summary>
    /// <param name="state">The block's own state.</param>
    /// <returns>The seed.</returns>
    protected static FakeIndexedRepo Seed(CodeIndexState state = CodeIndexState.Ok) =>
        new(Repo, state, Indexed, Symbols, Calls) { Unresolved = 2, Layer = state == CodeIndexState.LayerUnavailable ? "graph" : null, Code = state == CodeIndexState.LayerUnavailable ? "SND-CODEIDX-020" : null };

    /// <summary>An index at the map's commit is ok, and the head names the commit it describes.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Heads_at_the_maps_commit_is_ok()
    {
        const string test = nameof(Heads_at_the_maps_commit_is_ok);
        var source = Create([Seed()]);

        var head = Expect.Ok(await source.Heads(Repo, Indexed, None), test);

        Expect.That(head.State == CodeIndexState.Ok && head.Commit == Indexed && head.AsOfCommit is null, test, $"{head.State} {head.Commit} {head.AsOfCommit}");
    }

    /// <summary>An abbreviated map commit is the same commit.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_abbreviated_as_of_commit_is_the_same_commit()
    {
        const string test = nameof(An_abbreviated_as_of_commit_is_the_same_commit);
        var source = Create([Seed()]);

        var head = Expect.Ok(await source.Heads(Repo, Indexed[..7], None), test);

        Expect.That(head.State == CodeIndexState.Ok && head.AsOfCommit is null, test, $"{head.State} {head.AsOfCommit}");
    }

    /// <summary>A caller that names no map commit gets no mismatch.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task No_as_of_commit_never_mismatches()
    {
        const string test = nameof(No_as_of_commit_never_mismatches);
        var source = Create([Seed()]);

        var files = Expect.Ok(await source.FileSymbols(Repo, ["src/layout/SplitPane.ts"], asOf: null, None), test);

        Expect.That(files.Head.State == CodeIndexState.Ok, test, $"{files.Head.State}");
        Expect.That(files.Rows.All(r => r.State == SymbolsState.Ok && r.AsOfCommit is null), test, "a row is stale or names an as-of commit");
    }

    /// <summary>A map published ahead of its index, or behind it, reads stale with both commits on the head and on every row.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_index_at_another_commit_than_the_maps_is_stale_with_both_commits()
    {
        const string test = nameof(An_index_at_another_commit_than_the_maps_is_stale_with_both_commits);
        var source = Create([Seed()]);

        var head = Expect.Ok(await source.Heads(Repo, Other, None), test);
        var files = Expect.Ok(await source.FileSymbols(Repo, ["src/layout/SplitPane.ts"], Other, None), test);
        var found = Expect.Ok(await source.FindSymbols(Repo, new SymbolQuery("resize", Prefix: false, Max: 10), Other, None), test);
        var callers = Expect.Ok(await source.Callers(Repo, "src/layout/SplitPane.ts#setOrientation", 1, Other, None), test);
        var callees = Expect.Ok(await source.Callees(Repo, "src/layout/SplitPane.ts#setOrientation", 1, Other, None), test);

        Expect.That(head.State == CodeIndexState.Stale && head.Commit == Indexed && head.AsOfCommit == Other, test, $"{head.State} {head.Commit} {head.AsOfCommit}");
        Expect.That(files.Rows.Count == 1 && found.Rows.Count == 1 && callers.Rows.Count == 1 && callees.Rows.Count == 1, test, "the stale index still answers");
        Expect.That(files.Rows.All(r => (r.Commit, r.AsOfCommit, r.State) == (Indexed, Other, SymbolsState.Stale)), test, "a file row");
        Expect.That(found.Rows.All(r => (r.Commit, r.AsOfCommit, r.State) == (Indexed, Other, SymbolsState.Stale)), test, "a found symbol");
        Expect.That(callers.Rows.Concat(callees.Rows).All(r => (r.Commit, r.AsOfCommit, r.State) == (Indexed, Other, SymbolsState.Stale)), test, "a call row");
    }

    /// <summary>The index of an old commit whose head moved on (the build is not done) is stale with no as-of commit when the map is at the indexed one.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_stale_index_is_served_and_says_stale_without_an_as_of_commit()
    {
        const string test = nameof(A_stale_index_is_served_and_says_stale_without_an_as_of_commit);
        var source = Create([Seed(CodeIndexState.Stale)]);

        var files = Expect.Ok(await source.FileSymbols(Repo, ["src/layout/SplitPane.ts"], Indexed, None), test);

        Expect.That(files.Head.State == CodeIndexState.Stale && files.Head.AsOfCommit is null, test, $"{files.Head.State} {files.Head.AsOfCommit}");
        Expect.That(files.Rows.Count == 1, test, "one row");
        var row = files.Rows[0];
        Expect.That((row.Commit, row.AsOfCommit, row.State) == (Indexed, null, SymbolsState.Stale), test, $"{row.Commit} {row.AsOfCommit} {row.State}");
    }

    /// <summary>A repo with no index is an answer (missing, no rows) for every call, never an error; a repo the index does not list reads the same.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_repo_with_no_index_answers_missing_with_no_rows()
    {
        const string test = nameof(A_repo_with_no_index_answers_missing_with_no_rows);
        var source = Create([]);

        var head = Expect.Ok(await source.Heads(Repo, Indexed, None), test);
        var files = Expect.Ok(await source.FileSymbols(Repo, ["src/layout/SplitPane.ts"], Indexed, None), test);
        var found = Expect.Ok(await source.FindSymbols(Repo, new SymbolQuery("resize", Prefix: false, Max: 10), Indexed, None), test);
        var callers = Expect.Ok(await source.Callers(Repo, "src/layout/SplitPane.ts#resize", 1, Indexed, None), test);
        var callees = Expect.Ok(await source.Callees(Repo, "src/layout/SplitPane.ts#resize", 1, Indexed, None), test);

        Expect.That(head.State == CodeIndexState.Missing && head.Commit is null, test, $"{head.State} {head.Commit}");
        Expect.That(files.Rows.Count == 0 && found.Rows.Count == 0 && callers.Rows.Count == 0 && callees.Rows.Count == 0, test, "rows from nothing");
        Expect.That(files.Head.State == CodeIndexState.Missing && callers.Head.State == CodeIndexState.Missing, test, "every answer says missing");
    }

    /// <summary>A name search is exact, or by prefix, ordered by ref, and a 'path#name' search finds that symbol.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task FindSymbols_matches_by_name_prefix_and_ref()
    {
        const string test = nameof(FindSymbols_matches_by_name_prefix_and_ref);
        var source = Create([Seed()]);

        var exact = Expect.Ok(await source.FindSymbols(Repo, new SymbolQuery("res", Prefix: false, Max: 10), Indexed, None), test);
        var prefix = Expect.Ok(await source.FindSymbols(Repo, new SymbolQuery("res", Prefix: true, Max: 10), Indexed, None), test);
        var byRef = Expect.Ok(await source.FindSymbols(Repo, new SymbolQuery("src/tabs/Manager.ts#restoreLayout", Prefix: false, Max: 10), Indexed, None), test);
        var capped = Expect.Ok(await source.FindSymbols(Repo, new SymbolQuery("s", Prefix: true, Max: 1), Indexed, None), test);

        Expect.That(exact.Rows.Count == 0, test, "an exact name is not a prefix");
        Expect.That(prefix.Rows.Select(r => r.Site.Name).SequenceEqual(["resize", "restoreLayout"], StringComparer.Ordinal), test, string.Join(',', prefix.Rows.Select(r => r.Site.Name)));
        Expect.That(byRef.Rows.Select(r => r.Site.Ref).SequenceEqual(["src/tabs/Manager.ts#restoreLayout"], StringComparer.Ordinal), test, "by ref");
        Expect.That(capped.Rows.Count == 1, test, "the cap");
    }

    /// <summary>A file read gives one row per known file in the order asked, each with its symbols in file order; an unknown file has no row.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task FileSymbols_gives_a_row_per_known_file()
    {
        const string test = nameof(FileSymbols_gives_a_row_per_known_file);
        var source = Create([Seed()]);

        var answer = Expect.Ok(await source.FileSymbols(Repo, ["src/layout/SplitPane.ts", "src/none.ts", "src/layout/paneStore.ts"], Indexed, None), test);

        Expect.That(answer.Rows.Select(r => r.Path).SequenceEqual(["src/layout/SplitPane.ts", "src/layout/paneStore.ts"], StringComparer.Ordinal), test, string.Join(',', answer.Rows.Select(r => r.Path)));
        Expect.That(answer.Rows[0].Symbols.Select(s => s.Name).SequenceEqual(["setOrientation", "resize"], StringComparer.Ordinal), test, "file order");
        Expect.That(answer.Rows[0].Symbols[0].Signature == "setOrientation(o: Orientation): void" && answer.Rows[0].Symbols[0].Exported, test, "signature and export");
    }

    /// <summary>Callers go up the edges to the depth asked, each row says its level, basis, confidence and coverage, and the answer counts the call sites that resolved to nothing.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Callers_walk_to_the_depth_asked()
    {
        const string test = nameof(Callers_walk_to_the_depth_asked);
        var source = Create([Seed()]);

        var one = Expect.Ok(await source.Callers(Repo, "src/layout/paneStore.ts#save", 1, Indexed, None), test);
        var three = Expect.Ok(await source.Callers(Repo, "src/layout/paneStore.ts#save", 3, Indexed, None), test);

        Expect.That(one.Rows.Select(r => r.Symbol.Name).SequenceEqual(["resize"], StringComparer.Ordinal), test, "depth 1");
        Expect.That(three.Rows.Select(r => (r.Symbol.Name, r.Depth)).SequenceEqual([("resize", 1), ("setOrientation", 2), ("restoreLayout", 3)]), test, string.Join(',', three.Rows.Select(r => r.Symbol.Name)));
        Expect.That((one.Rows[0].Basis, one.Rows[0].Coverage, one.Rows[0].Confidence) == ("syntax", "partial", 0.6), test, "basis, coverage, confidence");
        Expect.That(three.Unresolved == 2, test, $"unresolved {three.Unresolved.ToString(CultureInfo.InvariantCulture)}");
    }

    /// <summary>Callees go down the edges.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Callees_walk_down_the_edges()
    {
        const string test = nameof(Callees_walk_down_the_edges);
        var source = Create([Seed()]);

        var two = Expect.Ok(await source.Callees(Repo, "src/tabs/Manager.ts#restoreLayout", 2, Indexed, None), test);

        Expect.That(two.Rows.Select(r => (r.Symbol.Name, r.Depth)).SequenceEqual([("setOrientation", 1), ("resize", 2)]), test, string.Join(',', two.Rows.Select(r => r.Symbol.Name)));
        Expect.That((two.Rows[0].Basis, two.Rows[0].Coverage) == ("semantic", "full"), test, "basis and coverage");
    }

    /// <summary>An index whose graph layer is down still answers, and says which layer.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_layer_that_is_down_is_named_on_the_head_and_the_index_still_answers()
    {
        const string test = nameof(A_layer_that_is_down_is_named_on_the_head_and_the_index_still_answers);
        var source = Create([Seed(CodeIndexState.LayerUnavailable)]);

        var callers = Expect.Ok(await source.Callers(Repo, "src/layout/SplitPane.ts#resize", 1, Indexed, None), test);

        Expect.That((callers.Head.State, callers.Head.Layer, callers.Head.Code) == (CodeIndexState.LayerUnavailable, "graph", "SND-CODEIDX-020") && callers.Rows.Count == 1, test, $"{callers.Head.State} {callers.Head.Layer} {callers.Rows.Count.ToString(CultureInfo.InvariantCulture)}");
    }

    /// <summary>A damaged or still-building index answers its state and no rows.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The test.</returns>
    [Theory]
    [InlineData(CodeIndexState.Damaged)]
    [InlineData(CodeIndexState.Building)]
    public async Task An_unreadable_index_answers_its_state_with_no_rows(CodeIndexState state)
    {
        const string test = nameof(An_unreadable_index_answers_its_state_with_no_rows);
        var source = Create([Seed(state)]);

        var files = Expect.Ok(await source.FileSymbols(Repo, ["src/layout/SplitPane.ts"], Indexed, None), test);

        Expect.That(files.Head.State == state && files.Rows.Count == 0, test, $"{files.Head.State} {files.Rows.Count}");
    }
}
