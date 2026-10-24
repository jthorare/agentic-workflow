using System.Text;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Results;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.QueryContext;

/// <summary>The diff shape through the facade (S-D): changed files from the merge base, their nodes, the cascade from them, and the paths no node claims.</summary>
public sealed class DiffQueryTests : IDisposable
{
    private const string Base = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Head = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string MergeBase = "cccccccccccccccccccccccccccccccccccccccc";
    private static readonly DiffRange _range = new(Base, Head);

    private readonly List<IDisposable> _disposables = [];

    public void Dispose() => _disposables.ForEach(d => d.Dispose());

    private static FakeRepoReader Reader(params (string Path, ChangeKind Change)[] files) =>
        new FakeRepoReader(MapWorld.Repos[0], MergeBase, new Dictionary<string, string>(StringComparer.Ordinal))
            .WithHistory([Base, Head, MergeBase], [new RepoChanges(Base, Head, MergeBase, [.. files.Select(f => new RepoChange(f.Path, f.Change))])]);

    private static readonly (string, ChangeKind)[] _usual =
    [
        ("src/layout/SplitPane.ts", ChangeKind.Modified), ("src/tabs/TabManager.ts", ChangeKind.Modified),
        ("src/commands/registry.ts", ChangeKind.Modified), ("README.md", ChangeKind.Added),
    ];

    private MapWorld World(IRepoReader? reader = null)
    {
        var world = new MapWorld(reader ?? Reader(_usual));
        _disposables.Add(world);
        return world;
    }

    private static ContextQuery Ask(ContextRepoName? repo = null, bool draft = false) => new(Text: null, Diff: _range, Repo: repo ?? ContextRepoName.From(FixtureMap.Repo), Draft: draft);

    private static ContextQuery AskWithoutRepo() => new(Text: null, Diff: _range);

    private static ContextScope SessionDenying(params string[] globs) =>
        new(Settings: System.Text.Json.JsonDocument.Parse("""{"sessions":{"enabled":true}}""").RootElement.Clone(),
            [new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes") { DenyPaths = [.. PathDenylist.Defaults, .. globs] }],
            new ContextCaller(ContextCallerKind.Session, ContextRepoName.From(FixtureMap.Repo), ContextClient.Http));

    [Fact, Trait("Lane", "unit")]
    public async Task Diff_lists_the_changed_files_maps_them_and_lists_the_rest_unmapped()
    {
        var world = World();
        _ = await world.ImportFixture();

        var result = await world.Engine.Query(Ask(ContextRepoName.From(FixtureMap.Repo)), MapWorld.Human, MapWorld.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        var diff = result.Value.Diff!;
        Assert.Equal((Base, Head, MergeBase), (diff.Base, diff.Head, diff.MergeBase));
        Assert.Equal(["README.md", "src/commands/registry.ts", "src/layout/SplitPane.ts", "src/tabs/TabManager.ts"], diff.Files.Select(f => f.Path), StringComparer.Ordinal);
        Assert.Equal(["README.md", "src/commands/registry.ts"], diff.Unmapped, StringComparer.Ordinal);
        var split = diff.Files.Single(f => string.Equals(f.Path, "src/layout/SplitPane.ts", StringComparison.Ordinal));
        Assert.Equal(DiffStatus.Mapped, split.Status);
        Assert.Equal(["feature:layout.split-pane", "feature:layout.split-pane.orientation"], split.Nodes.Select(n => n.Key), StringComparer.Ordinal);
        Assert.All(split.Nodes, n => Assert.True(n.Confirmed));
        Assert.Equal(new DiffCounts(4, 2, 0, 2, 0), diff.Counts);
        Assert.Equal((4, false), (diff.AnchorsTotal, diff.Truncated));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Diff_anchors_run_the_cascade_and_cite_like_the_symbol_shape()
    {
        var world = World();
        _ = await world.ImportFixture();

        var result = (await world.Engine.Query(Ask(), MapWorld.Human, MapWorld.None)).Value;

        Assert.Equal(["action:tab-manager.restore-layout", "feature:layout.split-pane", "feature:layout.split-pane.orientation", "feature:tabs.tab-manager"], result.Anchors, StringComparer.Ordinal);
        Assert.Contains(result.Sections, s => string.Equals(s.Name, "graph", StringComparison.Ordinal) && s.Items.Count >= 4);
        Assert.Contains(result.Sections, s => string.Equals(s.Name, "code", StringComparison.Ordinal));
        Assert.All(result.Sections.SelectMany(s => s.Items), i => Assert.NotEmpty(i.Cites));
        Assert.True(result.Cites.ContainsKey("C1"));
        Assert.NotNull(result.MapVersion);
        Assert.Equal(_range, result.Query.Diff);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_suggested_file_is_marked_unconfirmed_and_stays_out_of_unmapped()
    {
        var world = World(Reader(("src/tabs/TabGroups.ts", ChangeKind.Modified), ("src/tabs/Pinned.ts", ChangeKind.Added)));
        _ = await world.ImportYaml(MapWorld.TabsMap);

        var diff = (await world.Engine.Query(Ask(), MapWorld.Human, MapWorld.None)).Value.Diff!;

        var groups = diff.Files.Single(f => string.Equals(f.Path, "src/tabs/TabGroups.ts", StringComparison.Ordinal));
        Assert.Equal(DiffStatus.Suggested, groups.Status);
        Assert.All(groups.Nodes, n => Assert.False(n.Confirmed));
        Assert.Equal(["src/tabs/Pinned.ts"], diff.Unmapped, StringComparer.Ordinal);
        Assert.Equal(1, diff.Counts.Suggested);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Diff_without_repo_uses_the_only_mapped_repo()
    {
        var world = World();
        _ = await world.ImportFixture();
        var only = MapWorld.HumanWith(MapWorld.OnlyFixtureRepo);

        var result = await world.Engine.Query(AskWithoutRepo(), only, MapWorld.None);

        Assert.Equal(FixtureMap.Repo, result.Value.Diff!.Repo.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Diff_without_repo_is_006_when_two_are_mapped()
    {
        var world = World();
        _ = await world.ImportFixture();

        var result = await world.Engine.Query(AskWithoutRepo(), MapWorld.Human, MapWorld.None);

        Assert.Equal(ContextErrors.QueryInvalid.Code, result.Error.Code);
        Assert.Contains("name the repo", result.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("nope")]
    [InlineData("other")]
    public async Task Diff_for_a_repo_outside_the_scope_is_045_with_one_answer(string repo)
    {
        var world = World();
        _ = await world.ImportFixture();
        var human = MapWorld.HumanWith(MapWorld.OnlyFixtureRepo);

        var asHuman = await world.Engine.Query(Ask(ContextRepoName.From(repo)), human, MapWorld.None);
        var asSession = await world.Engine.Query(Ask(ContextRepoName.From(repo)), MapWorld.Session(FixtureMap.Repo), MapWorld.None);

        Assert.Equal(ContextErrors.RepoOutsideScope.Code, asHuman.Error.Code);
        Assert.Equal(ContextErrors.RepoOutsideScope.Code, asSession.Error.Code);
        Assert.Equal(asHuman.Error.Message, asSession.Error.Message, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Diff_with_no_map_is_030()
    {
        var world = World();

        var result = await world.Engine.Query(Ask(ContextRepoName.From(FixtureMap.Repo)), MapWorld.Human, MapWorld.None);

        Assert.Equal(ContextErrors.NoMap.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Diff_draft_without_a_draft_is_044()
    {
        var world = World();
        _ = await world.ImportFixture();

        var result = await world.Engine.Query(Ask(ContextRepoName.From(FixtureMap.Repo), draft: true), MapWorld.Human, MapWorld.None);

        Assert.Equal(ContextErrors.NoDraftMap.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Diff_with_a_source_down_degrades_the_sections_and_keeps_the_diff_block()
    {
        var world = World();
        _ = await world.ImportFixture();
        world.Docs.Failure = ContextErrors.SourceUnavailable.With("docs are down");

        var result = await world.Engine.Query(Ask(), MapWorld.Human, MapWorld.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Contains(result.Value.Degraded, d => string.Equals(d.Code, "SND-CONTEXT-010", StringComparison.Ordinal));
        Assert.Equal(4, result.Value.Diff!.Files.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Diff_with_a_commit_the_clone_lacks_is_110_and_without_a_merge_base_111()
    {
        var world = World(new FakeRepoReader(MapWorld.Repos[0], MergeBase, new Dictionary<string, string>(StringComparer.Ordinal)).WithHistory([Base, MergeBase], []));
        _ = await world.ImportFixture();

        var missing = await world.Engine.Query(Ask(), MapWorld.Human, MapWorld.None);
        var unrelated = await world.Engine.Query(new ContextQuery(Text: null, Diff: new DiffRange(Base, MergeBase), Repo: ContextRepoName.From(FixtureMap.Repo)), MapWorld.Human, MapWorld.None);

        Assert.Equal(ContextErrors.CommitUnknown.Code, missing.Error.Code);
        Assert.Equal(ContextErrors.NoMergeBase.Code, unrelated.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Session_diff_counts_denied_paths_and_never_names_them()
    {
        var world = World(Reader(("src/layout/SplitPane.ts", ChangeKind.Modified), ("docs/decisions/0001.md", ChangeKind.Added), (".env", ChangeKind.Modified)));
        _ = await world.ImportFixture();

        var result = (await world.Engine.Query(Ask(), SessionDenying("docs/decisions/**"), MapWorld.None)).Value;

        Assert.Equal(["src/layout/SplitPane.ts"], result.Diff!.Files.Select(f => f.Path), StringComparer.Ordinal);
        Assert.Empty(result.Diff.Unmapped);
        Assert.Equal(new DiffCounts(3, 1, 0, 0, 2), result.Diff.Counts);
        var json = Encoding.UTF8.GetString(Kernel.Json.AwJson.Serialize(result, Kernel.Json.AwJson.CreateOptions(Contract.ContractJsonContext.Default)));
        Assert.DoesNotContain("0001.md", json, StringComparison.Ordinal);
        Assert.DoesNotContain(".env", json, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_changed_path_is_scrubbed_like_node_text_for_every_caller()
    {
        var planted = $"src/{FakeScrubber.Secret}.ts";
        var world = World(Reader((planted, ChangeKind.Added)));
        _ = await world.ImportFixture();

        var session = (await world.Engine.Query(Ask(), SessionDenying(), MapWorld.None)).Value.Diff!;
        var human = (await world.Engine.Query(Ask(ContextRepoName.From(FixtureMap.Repo)), MapWorld.Human, MapWorld.None)).Value.Diff!;

        Assert.DoesNotContain(FakeScrubber.Secret, session.Files[0].Path, StringComparison.Ordinal);
        Assert.DoesNotContain(FakeScrubber.Secret, session.Unmapped[0], StringComparison.Ordinal);
        Assert.DoesNotContain(FakeScrubber.Secret, human.Files[0].Path, StringComparison.Ordinal);
        Assert.DoesNotContain(FakeScrubber.Secret, human.Unmapped[0], StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Diff_with_sessions_disabled_is_020()
    {
        var world = World();
        _ = await world.ImportFixture();

        var result = await world.Engine.Query(Ask(), MapWorld.Session(FixtureMap.Repo, enabled: false), MapWorld.None);

        Assert.Equal(ContextErrors.SessionsDisabled.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_empty_range_answers_with_no_files_and_no_anchors()
    {
        var world = World(Reader());
        _ = await world.ImportFixture();

        var result = (await world.Engine.Query(Ask(), MapWorld.Human with { Repos = [MapWorld.Repos[0]] }, MapWorld.None)).Value;

        Assert.Empty(result.Diff!.Files);
        Assert.Empty(result.Anchors);
        Assert.Equal(new DiffCounts(0, 0, 0, 0, 0), result.Diff.Counts);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Diff_of_PR3_lists_its_files_maps_SplitPane_and_lists_the_rest_unmapped()
    {
        var repo = Testing.Fixtures.FixtureRepo.Build();
        _disposables.Add(repo);
        var pr = repo.PullRequests.Single(p => p.Number == 3);
        var gitBase = Git(repo, "rev-parse", pr.MergeCommit + "^1");
        var gitHead = Git(repo, "rev-parse", pr.MergeCommit + "^2");
        var world = World(new Context.Infrastructure.Extract.GitRepoReader(new Context.Infrastructure.Git.GitCli()));
        _ = await world.ImportFixture();
        var scope = MapWorld.HumanWith(MapWorld.OnlyFixtureRepo, [new ContextRepo(ContextRepoName.From(FixtureMap.Repo), repo.Path)]);

        var result = await world.Engine.Query(new ContextQuery(Text: null, Diff: new DiffRange(gitBase, gitHead)), scope, MapWorld.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        var diff = result.Value.Diff!;
        Assert.Equal(Git(repo, "merge-base", gitBase, gitHead), diff.MergeBase);
        Assert.Equal(pr.Files, diff.Files.Select(f => f.Path), StringComparer.Ordinal);
        Assert.Equal(DiffStatus.Mapped, diff.Files.Single(f => string.Equals(f.Path, "src/layout/SplitPane.ts", StringComparison.Ordinal)).Status);
        Assert.DoesNotContain("src/layout/SplitPane.ts", diff.Unmapped, StringComparer.Ordinal);
        Assert.Contains("src/commands/registry.ts", diff.Unmapped, StringComparer.Ordinal);
        Assert.Contains("src/keybindings/defaults.ts", diff.Unmapped, StringComparer.Ordinal);
        Assert.Equal(["feature:layout.split-pane", "feature:layout.split-pane.orientation"], result.Value.Anchors, StringComparer.Ordinal);
        Assert.Contains(result.Value.Sections, s => string.Equals(s.Name, "affects", StringComparison.Ordinal));
    }

    private static string Git(Testing.Fixtures.FixtureRepo repo, params string[] args)
    {
        var start = new System.Diagnostics.ProcessStartInfo("git") { RedirectStandardOutput = true, UseShellExecute = false };
        foreach (var name in start.Environment.Keys.Where(k => k.StartsWith("GIT_", StringComparison.Ordinal)).ToList())
        {
            _ = start.Environment.Remove(name);   // why: like GitRunner, a caller's GIT_DIR must not redirect the helper
        }

        start.ArgumentList.Add("-C");
        start.ArgumentList.Add(repo.Path);
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        using var git = System.Diagnostics.Process.Start(start)!;   // why: git is on the test machine's PATH
        var output = git.StandardOutput.ReadToEnd().Trim();
        git.WaitForExit();
        return output;
    }
}
