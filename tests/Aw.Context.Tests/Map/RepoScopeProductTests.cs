using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Context.Testing.Graph;

namespace Aw.Context.Tests.Map;

// Regression for #1565: a stored, confirmed map attaches its product nodes only by `action -on-> surface` and
// `code|action -reads|writes-> thing` edges, but the repo cut every repo-named read applies follows only
// implements/tags + part-of + linked intent + concepts — so it drops every action and thing. The scope must keep
// them (cascade §6.1), and must still not leak another repo's.
public sealed class RepoScopeProductTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_sessions_scope_keeps_the_actions_and_things_attached_to_its_own_nodes()
    {
        var graph = new GraphBuilder()
            .Node("code:src/a/Schedule.tsx", "Schedule.tsx")
            .Node("surface:screen/schedule", "Schedule screen", Provenance.Deterministic)
            .Node("feature:self-scheduling", "Self scheduling")
            .Node("action:schedule/book-shift", "Book a shift", Provenance.Model, 0.8)
            .Node("thing:shift-state", "Shift state", Provenance.Model, 0.8)
            .Node("thing:staffing-baseline", "Staffing baseline", Provenance.Model, 0.8)
            .Edge("code:src/a/Schedule.tsx", EdgeType.Implements, "surface:screen/schedule", evidence: "repo:web/src/a/Schedule.tsx")
            .Edge("surface:screen/schedule", EdgeType.PartOf, "feature:self-scheduling")
            .Edge("action:schedule/book-shift", EdgeType.On, "surface:screen/schedule")
            .Edge("code:src/a/Schedule.tsx", EdgeType.Writes, "thing:shift-state", evidence: "repo:web/src/a/Schedule.tsx")
            .Edge("code:src/a/Schedule.tsx", EdgeType.Reads, "thing:staffing-baseline", evidence: "repo:web/src/a/Schedule.tsx")
            .Build();

        var scoped = graph.ScopedTo(Context.Core.Engine.ContextRepoName.From("web"));

        Assert.Equal(
            ["action:schedule/book-shift", "code:src/a/Schedule.tsx", "feature:self-scheduling", "surface:screen/schedule", "thing:shift-state", "thing:staffing-baseline"],
            scoped.Nodes.Select(n => n.Key.Value).Order(StringComparer.Ordinal),
            StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Another_repos_actions_and_things_stay_out()
    {
        var graph = new GraphBuilder()
            .Node("code:src/a/Schedule.tsx", "Schedule.tsx")
            .Node("surface:screen/schedule", "Schedule screen", Provenance.Deterministic)
            .Node("feature:self-scheduling", "Self scheduling")
            .Node("action:schedule/book-shift", "Book a shift", Provenance.Model, 0.8)
            .Node("thing:shift-state", "Shift state", Provenance.Model, 0.8)
            .Node("thing:staffing-baseline", "Staffing baseline", Provenance.Model, 0.8)
            .Node("surface:screen/roster", "Roster screen", Provenance.Deterministic)
            .Node("code:src/b/Roster.tsx", "Roster.tsx")
            .Node("action:roster/assign", "Assign a shift", Provenance.Model, 0.8)
            .Node("thing:roster-state", "Roster state", Provenance.Model, 0.8)
            .Edge("code:src/a/Schedule.tsx", EdgeType.Implements, "surface:screen/schedule", evidence: "repo:web/src/a/Schedule.tsx")
            .Edge("surface:screen/schedule", EdgeType.PartOf, "feature:self-scheduling")
            .Edge("action:schedule/book-shift", EdgeType.On, "surface:screen/schedule")
            .Edge("code:src/a/Schedule.tsx", EdgeType.Writes, "thing:shift-state", evidence: "repo:web/src/a/Schedule.tsx")
            .Edge("code:src/a/Schedule.tsx", EdgeType.Reads, "thing:staffing-baseline", evidence: "repo:web/src/a/Schedule.tsx")
            .Edge("code:src/b/Roster.tsx", EdgeType.Implements, "surface:screen/roster", evidence: "repo:other/src/b/Roster.tsx")
            .Edge("action:roster/assign", EdgeType.On, "surface:screen/roster")
            .Edge("code:src/b/Roster.tsx", EdgeType.Writes, "thing:roster-state", evidence: "repo:other/src/b/Roster.tsx")
            .Build();

        var scoped = graph.ScopedTo(Context.Core.Engine.ContextRepoName.From("web"));

        var keys = scoped.Nodes.Select(n => n.Key.Value).ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain("action:roster/assign", keys, StringComparer.Ordinal);
        Assert.DoesNotContain("thing:roster-state", keys, StringComparer.Ordinal);
        Assert.DoesNotContain("surface:screen/roster", keys, StringComparer.Ordinal);
        Assert.DoesNotContain("code:src/b/Roster.tsx", keys, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_shared_features_rolled_up_reads_and_writes_bring_no_other_repos_things()
    {
        var graph = new GraphBuilder()
            .Node("feature:sharing", "Sharing")
            .Node("code:src/a/Share.tsx", "Share.tsx")
            .Node("code:src/b/Upload.cs", "Upload.cs")
            .Node("thing:share-link", "Share link", Provenance.Model, 0.8)
            .Node("thing:upload-bucket", "Upload bucket", Provenance.Model, 0.8)
            .Edge("code:src/a/Share.tsx", EdgeType.Implements, "feature:sharing", evidence: "repo:web/src/a/Share.tsx")
            .Edge("code:src/b/Upload.cs", EdgeType.Implements, "feature:sharing", evidence: "repo:other/src/b/Upload.cs")
            .Edge("code:src/a/Share.tsx", EdgeType.Writes, "thing:share-link", evidence: "repo:web/src/a/Share.tsx")
            .Edge("code:src/b/Upload.cs", EdgeType.Writes, "thing:upload-bucket", evidence: "repo:other/src/b/Upload.cs")
            .Edge("feature:sharing", EdgeType.Writes, "thing:share-link")
            .Edge("feature:sharing", EdgeType.Writes, "thing:upload-bucket")   // the roll-up of the other repo's file (FeatureRollup)
            .Build();

        var keys = graph.ScopedTo(Context.Core.Engine.ContextRepoName.From("web")).Nodes.Select(n => n.Key.Value).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("thing:share-link", keys, StringComparer.Ordinal);
        Assert.DoesNotContain("thing:upload-bucket", keys, StringComparer.Ordinal);
    }
}
