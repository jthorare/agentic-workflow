using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Context.Core.Review;
using Aw.Context.Testing.Graph;

namespace Aw.Context.Tests.Map;

/// <summary>Spec 2026-10-27 §2.3 "Who reads Implements": repo scope, the deny and demote cuts and the review queue read a 'tags' link, and home features are not proposals.</summary>
public sealed class TagEdgeReadersTests
{
    private static readonly DateTimeOffset _at = DateTimeOffset.Parse("2026-10-27T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    private static readonly string[] _groups = ["feature:work.src", "feature:work.src.a"];
    private static readonly string[] _gone = ["feature:web.secret", "feature:tag:web:private", "code:private/p.ts"];
    private static readonly string[] _both = ["feature:web.secret", "feature:web"];

    private static GraphBuilder Tagged() => new GraphBuilder()
        .Node("feature:tag:web:auth", provenance: Provenance.Model, confidence: 0.7).Node("feature:tag:app:auth", provenance: Provenance.Model, confidence: 0.7)
        .Edge("code:src/a.ts", EdgeType.Tags, "feature:tag:web:auth", provenance: Provenance.Model, confidence: 0.7, evidence: "repo:web/src/a.ts")
        .Edge("code:src/b.ts", EdgeType.Tags, "feature:tag:app:auth", provenance: Provenance.Model, confidence: 0.7, evidence: "repo:app/src/b.ts");

    [Fact, Trait("Lane", "unit")]
    public void A_tag_feature_belongs_to_the_repo_whose_files_tag_it_and_is_invisible_to_the_other()
    {
        var graph = Tagged().Build();

        var web = RepoScope.NodesFor(graph, ContextRepoName.From("web"));

        Assert.Contains("feature:tag:web:auth", web);
        Assert.Contains("code:src/a.ts", web);
        Assert.DoesNotContain("feature:tag:app:auth", web);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_review_queue_holds_no_home_or_group_feature_and_lists_tags_as_suggestions()
    {
        var graph = Tagged()
            .Node("feature:work.src", provenance: Provenance.Deterministic).Node("feature:work.src.a", provenance: Provenance.Deterministic)
            .Edge("feature:work.src.a", EdgeType.PartOf, "feature:work.src", provenance: Provenance.Deterministic)
            .Edge("code:src/a.ts", EdgeType.Implements, "feature:work.src.a", provenance: Provenance.Deterministic, evidence: "repo:web/src/a.ts")
            .Build();

        var queue = ReviewBacklog.Build("v1", graph, []);

        Assert.DoesNotContain(queue.Entries, e => _groups.Contains(e.Node.Value, StringComparer.Ordinal));
        Assert.DoesNotContain(queue.Entries, e => e.Link is null && e.Node.Value == "code:src/a.ts");
        Assert.Contains(queue.Entries, e => e.Link is not null && e.Node.Value == "code:src/a.ts");
        Assert.Contains(queue.Entries, e => e.Link is null && e.Node.Value == "feature:tag:web:auth");
    }

    [Fact, Trait("Lane", "unit")]
    public void A_demoted_files_tags_are_dropped_like_its_implements_edges()
    {
        var graph = Tagged().Build();
        var demoting = new ContextRepo(ContextRepoName.From("web"), "/web") { DemotePaths = ["src/a.ts"] };

        var (cut, _) = MapAccess.WithoutDemoted(graph, [], [demoting], _at);

        Assert.DoesNotContain(cut.Edges, e => e.From.Value == "code:src/a.ts");
        Assert.Contains(cut.Edges, e => e.From.Value == "code:src/b.ts");
    }

    [Fact, Trait("Lane", "unit")]
    public void A_denied_directory_takes_its_home_feature_its_group_and_its_tag_feature_with_it()
    {
        var graph = Tagged()
            .Node("feature:web.secret", provenance: Provenance.Deterministic).Node("feature:web", provenance: Provenance.Deterministic).Node("feature:web.src", provenance: Provenance.Deterministic)
            .Node("feature:tag:web:private", provenance: Provenance.Model, confidence: 0.6)
            .Edge("feature:web.secret", EdgeType.PartOf, "feature:web", provenance: Provenance.Deterministic)
            .Edge("feature:web.src", EdgeType.PartOf, "feature:web", provenance: Provenance.Deterministic)
            .Edge("code:src/a.ts", EdgeType.Implements, "feature:web.src", provenance: Provenance.Deterministic, evidence: "repo:web/src/a.ts")
            .Edge("code:private/p.ts", EdgeType.Implements, "feature:web.secret", provenance: Provenance.Deterministic, evidence: "repo:web/private/p.ts")
            .Edge("code:private/p.ts", EdgeType.Tags, "feature:tag:web:private", provenance: Provenance.Model, confidence: 0.6, evidence: "repo:web/private/p.ts")
            .Build();
        var web = new ContextRepo(ContextRepoName.From("web"), "/web") { DenyPaths = ["private/**"] };

        var (cut, _) = MapAccess.WithoutDenied(graph, [], [web], [web], _at);

        Assert.DoesNotContain(cut.Nodes, n => _gone.Contains(n.Key.Value, StringComparer.Ordinal));
        Assert.Contains(cut.Nodes, n => n.Key.Value == "feature:web.src");
        Assert.Contains(cut.Nodes, n => n.Key.Value == "feature:web");
    }

    [Fact, Trait("Lane", "unit")]
    public void A_group_whose_every_child_is_denied_goes_too_and_a_group_with_a_surviving_child_stays()
    {
        var graph = new GraphBuilder()
            .Node("feature:web", provenance: Provenance.Deterministic).Node("feature:web.secret", provenance: Provenance.Deterministic)
            .Edge("feature:web.secret", EdgeType.PartOf, "feature:web", provenance: Provenance.Deterministic)
            .Edge("code:private/p.ts", EdgeType.Implements, "feature:web.secret", provenance: Provenance.Deterministic, evidence: "repo:web/private/p.ts")
            .Build();
        var web = new ContextRepo(ContextRepoName.From("web"), "/web") { DenyPaths = ["private/**"] };

        var (cut, _) = MapAccess.WithoutDenied(graph, [], [web], [web], _at);

        Assert.DoesNotContain(cut.Nodes, n => _both.Contains(n.Key.Value, StringComparer.Ordinal));
    }
}
