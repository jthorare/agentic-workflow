using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Results;
using Aw.Context.Testing.Graph;

namespace Aw.Context.Tests.QueryContext;

public sealed class DiffAnchorsTests
{
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";
    private static readonly ContextRepo _repo = new(ContextRepoName.From("aw-split-panes"), "/work/aw-split-panes");

    private static GraphBuilder Map() => new GraphBuilder()
        .Edge("code:src/layout/SplitPane.ts", EdgeType.Implements, "feature:layout.split-pane.orientation", provenance: Provenance.Human)
        .Edge("code:src/layout/SplitPane.ts", EdgeType.Implements, "feature:layout.split-pane", provenance: Provenance.Human)
        .Edge("code:src/layout/SplitPane.ts", EdgeType.Tests, "feature:layout.split-pane.tests", provenance: Provenance.Human)
        .Edge("code:src/tabs/TabGroups.ts", EdgeType.Implements, "feature:tabs.tab-groups", provenance: Provenance.Model, confidence: 0.7)
        .Edge("code:src/tabs/TabManager.ts", EdgeType.Implements, "feature:tabs.tab-manager", provenance: Provenance.Human)
        .Edge("code:src/tabs/TabManager.ts", EdgeType.Implements, "feature:tabs.tab-groups", provenance: Provenance.Human);

    private static RepoChanges Changes(params (string Path, ChangeKind Change)[] files) =>
        new(Sha, Sha, Sha, [.. files.Select(f => new RepoChange(f.Path, f.Change))]);

    private static (DiffPart Part, IReadOnlyList<GraphNode> Anchors) Build(RepoChanges changes, ContextRepo? repo = null, GraphBuilder? map = null) =>
        DiffAnchors.Build(changes, repo ?? _repo, (map ?? Map()).Build());

    [Fact, Trait("Lane", "unit")]
    public void Changed_file_status_matches_FileLinks()
    {
        var (part, _) = Build(Changes(("src/layout/SplitPane.ts", ChangeKind.Modified), ("src/tabs/TabGroups.ts", ChangeKind.Modified), ("src/commands/registry.ts", ChangeKind.Added)));

        Assert.Equal(["src/commands/registry.ts", "src/layout/SplitPane.ts", "src/tabs/TabGroups.ts"], part.Files.Select(f => f.Path), StringComparer.Ordinal);
        Assert.Equal([DiffStatus.Unmapped, DiffStatus.Mapped, DiffStatus.Suggested], part.Files.Select(f => f.Status));
        Assert.Equal([ChangeKind.Added, ChangeKind.Modified, ChangeKind.Modified], part.Files.Select(f => f.Change));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_path_two_repos_share_claims_only_the_nodes_the_diffed_repo_implements()
    {
        var map = new GraphBuilder()
            .Edge("code:src/shared.ts", EdgeType.Implements, "feature:mine", evidence: "repo:aw-split-panes/src/shared.ts")
            .Edge("code:src/shared.ts", EdgeType.Implements, "feature:theirs", evidence: "repo:other/src/shared.ts")
            .Edge("code:src/theirs-only.ts", EdgeType.Implements, "feature:theirs", evidence: "repo:other/src/theirs-only.ts");

        var (part, anchors) = Build(Changes(("src/shared.ts", ChangeKind.Modified), ("src/theirs-only.ts", ChangeKind.Modified)), map: map);

        var shared = part.Files.Single(f => string.Equals(f.Path, "src/shared.ts", StringComparison.Ordinal));
        Assert.Equal(["feature:mine"], shared.Nodes.Select(n => n.Key), StringComparer.Ordinal);
        Assert.Equal(["feature:mine"], anchors.Select(a => a.Key.Value), StringComparer.Ordinal);
        Assert.Equal(["src/theirs-only.ts"], part.Unmapped, StringComparer.Ordinal);
        Assert.Equal(new DiffCounts(2, 1, 0, 1, 0), part.Counts);
    }

    [Fact, Trait("Lane", "unit")]
    public void Unmapped_lists_exactly_the_unmapped_files()
    {
        var (part, _) = Build(Changes(("src/z.ts", ChangeKind.Added), ("src/layout/SplitPane.ts", ChangeKind.Modified), ("src/a.ts", ChangeKind.Deleted), ("src/tabs/TabGroups.ts", ChangeKind.Modified)));

        Assert.Equal(["src/a.ts", "src/z.ts"], part.Unmapped, StringComparer.Ordinal);
        Assert.Equal(part.Files.Where(f => f.Status == DiffStatus.Unmapped).Select(f => f.Path), part.Unmapped, StringComparer.Ordinal);
        Assert.Equal(new DiffCounts(Changed: 4, Mapped: 1, Suggested: 1, Unmapped: 2, Denied: 0), part.Counts);
    }

    [Fact, Trait("Lane", "unit")]
    public void Suggested_file_nodes_are_marked_unconfirmed_and_not_unmapped()
    {
        var (part, _) = Build(Changes(("src/tabs/TabGroups.ts", ChangeKind.Modified)));

        var file = Assert.Single(part.Files);
        Assert.Equal(DiffStatus.Suggested, file.Status);
        var node = Assert.Single(file.Nodes);
        Assert.Equal(("feature:tabs.tab-groups", false), (node.Key, node.Confirmed));
        Assert.Empty(part.Unmapped);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_files_nodes_are_its_implements_targets_confirmed_first_then_by_key_and_never_its_tests_edges()
    {
        var (part, _) = Build(Changes(("src/layout/SplitPane.ts", ChangeKind.Modified)));

        Assert.Equal(["feature:layout.split-pane", "feature:layout.split-pane.orientation"], part.Files[0].Nodes.Select(n => n.Key), StringComparer.Ordinal);
        Assert.All(part.Files[0].Nodes, n => Assert.True(n.Confirmed));
    }

    [Fact, Trait("Lane", "unit")]
    public void Anchors_are_confirmed_first_then_by_key()
    {
        var (part, anchors) = Build(Changes(("src/tabs/TabGroups.ts", ChangeKind.Modified), ("src/layout/SplitPane.ts", ChangeKind.Modified), ("src/tabs/TabManager.ts", ChangeKind.Modified)));

        // tab-groups is reached by a confirmed edge from TabManager.ts, so it is confirmed overall.
        Assert.Equal(["feature:layout.split-pane", "feature:layout.split-pane.orientation", "feature:tabs.tab-groups", "feature:tabs.tab-manager"], anchors.Select(a => a.Key.Value), StringComparer.Ordinal);
        Assert.Equal(4, part.AnchorsTotal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_node_only_a_suggested_edge_reaches_comes_after_every_confirmed_one()
    {
        var map = new GraphBuilder()
            .Edge("code:src/a.ts", EdgeType.Implements, "feature:z.confirmed", provenance: Provenance.Human)
            .Edge("code:src/b.ts", EdgeType.Implements, "feature:a.suggested", provenance: Provenance.Model, confidence: 0.6);

        var (_, anchors) = Build(Changes(("src/a.ts", ChangeKind.Modified), ("src/b.ts", ChangeKind.Modified)), map: map);

        Assert.Equal(["feature:z.confirmed", "feature:a.suggested"], anchors.Select(a => a.Key.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_claimed_anchor_is_used_however_many_there_are()
    {
        var map = new GraphBuilder();
        var files = new List<(string, ChangeKind)>();
        for (var i = 0; i < 25; i++)
        {
            var n = i.ToString("00", CultureInfo.InvariantCulture);
            _ = map.Edge($"code:src/f{n}.ts", EdgeType.Implements, $"feature:n{n}", provenance: Provenance.Human);
            files.Add(($"src/f{n}.ts", ChangeKind.Modified));
        }

        var (part, anchors) = Build(Changes([.. files]), map: map);

        Assert.Equal(25, anchors.Count);
        Assert.Equal("feature:n00", anchors[0].Key.Value);
        Assert.Equal("feature:n24", anchors[^1].Key.Value);
        Assert.Equal((25, 25), (part.AnchorsTotal, part.Files.Count));
        Assert.All(part.Files, f => Assert.Single(f.Nodes));
    }

    [Fact, Trait("Lane", "unit")]
    public void Denied_paths_are_counted_never_listed()
    {
        var repo = _repo with { DenyPaths = [.. PathDenylist.Defaults, "docs/decisions/**"] };

        var (part, anchors) = Build(Changes(("docs/decisions/0001-orientation.md", ChangeKind.Added), (".env", ChangeKind.Added), ("src/layout/SplitPane.ts", ChangeKind.Modified)), repo);

        Assert.Equal(["src/layout/SplitPane.ts"], part.Files.Select(f => f.Path), StringComparer.Ordinal);
        Assert.Empty(part.Unmapped);
        Assert.Equal(new DiffCounts(Changed: 3, Mapped: 1, Suggested: 0, Unmapped: 0, Denied: 2), part.Counts);
        Assert.Equal(2, anchors.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void Deleted_file_still_maps_through_the_map_it_had()
    {
        var (part, anchors) = Build(Changes(("src/layout/SplitPane.ts", ChangeKind.Deleted)));

        var file = Assert.Single(part.Files);
        Assert.Equal((ChangeKind.Deleted, DiffStatus.Mapped), (file.Change, file.Status));
        Assert.Equal(2, anchors.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void Over_1000_changed_files_are_all_listed_and_never_cut()
    {
        var files = Enumerable.Range(0, 1_200).Select(i => ("gen/f" + i.ToString("0000", CultureInfo.InvariantCulture) + ".ts", ChangeKind.Added)).ToArray();

        var (part, _) = Build(Changes(files));

        Assert.False(part.Truncated);
        Assert.Equal(1_200, part.Files.Count);
        Assert.Equal(1_200, part.Unmapped.Count);
        Assert.Equal(new DiffCounts(Changed: 1_200, Mapped: 0, Suggested: 0, Unmapped: 1_200, Denied: 0), part.Counts);
        Assert.Equal("gen/f0000.ts", part.Files[0].Path);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_changes_is_an_empty_answer_that_still_names_the_range()
    {
        var (part, anchors) = Build(Changes());

        Assert.Empty(part.Files);
        Assert.Empty(anchors);
        Assert.Equal((new DiffCounts(0, 0, 0, 0, 0), false, 0), (part.Counts, part.Truncated, part.AnchorsTotal));
        Assert.Equal((_repo.Name, Sha, Sha, Sha), (part.Repo, part.Base, part.Head, part.MergeBase));
    }

    [Fact, Trait("Lane", "unit")]
    public void Files_come_in_ordinal_path_order_whatever_order_git_gave()
    {
        var (part, _) = Build(Changes(("src/b.ts", ChangeKind.Added), ("Src/a.ts", ChangeKind.Added), ("src/a.ts", ChangeKind.Added)));

        Assert.Equal(["Src/a.ts", "src/a.ts", "src/b.ts"], part.Files.Select(f => f.Path), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_counts_always_close()
    {
        var repo = _repo with { DenyPaths = [.. PathDenylist.Defaults, "secret/**"] };

        var (part, _) = Build(Changes(("secret/a", ChangeKind.Added), ("src/layout/SplitPane.ts", ChangeKind.Modified), ("src/tabs/TabGroups.ts", ChangeKind.Modified), ("x.md", ChangeKind.Added)), repo);

        var c = part.Counts;
        Assert.Equal(c.Changed, c.Mapped + c.Suggested + c.Unmapped + c.Denied);
    }
}
