using System.Globalization;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Surfaces;

namespace Aw.Context.Tests.Surfaces;

/// <summary>Which actions of a build are asked about and how lookalikes are grouped (spec 6.9, apply step).</summary>
[Trait("Lane", "unit")]
public sealed class SurfaceCandidatesTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 29, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Two_handlers_in_one_file_make_one_group()
    {
        var found = Map(Action("action:a", "Grant", "repo:r/a.ts"), Action("action:b", "Revoke", "repo:r/a.ts"));

        var group = Assert.Single(SurfaceCandidates.Groups(found, []));

        Assert.Equal(["action:a", "action:b"], group.Candidates.Select(c => c.NodeKey), StringComparer.Ordinal);
    }

    [Fact]
    public void The_same_name_in_two_files_joins_ignoring_case_and_spaces_at_the_ends()
    {
        var found = Map(Action("action:a", "Grant access", "repo:r/a.ts"), Action("action:b", " grant ACCESS ", "repo:r/b.ts"));

        _ = Assert.Single(SurfaceCandidates.Groups(found, []));
    }

    [Fact]
    public void Lookalikes_join_through_a_third_action()
    {
        var found = Map(Action("action:a", "One", "repo:r/a.ts"), Action("action:b", "Two", "repo:r/a.ts", "repo:r/b.ts"), Action("action:c", "Three", "repo:r/b.ts"));

        var group = Assert.Single(SurfaceCandidates.Groups(found, []));

        Assert.Equal(3, group.Candidates.Count);
    }

    [Fact]
    public void A_loner_is_a_group_of_one_and_groups_are_ordered_by_first_key()
    {
        var found = Map(Action("action:z", "Zed", "repo:r/z.ts"), Action("action:a", "Alpha", "repo:r/a.ts"));

        var groups = SurfaceCandidates.Groups(found, []);

        Assert.Equal(["action:a", "action:z"], groups.Select(g => g.Candidates.Single().NodeKey), StringComparer.Ordinal);
    }

    [Fact]
    public void Seven_lookalikes_give_groups_of_three_three_and_one()
    {
        var found = Map([.. Enumerable.Range(0, 7).Select(i => Action("action:x" + i.ToString(CultureInfo.InvariantCulture), "Same", "repo:r/f" + i.ToString(CultureInfo.InvariantCulture) + ".ts"))]);

        var groups = SurfaceCandidates.Groups(found, []);

        Assert.Equal([3, 3, 1], groups.Select(g => g.Candidates.Count));
    }

    [Fact]
    public void Only_deterministic_actions_are_candidates()
    {
        var found = Map(
            Action("action:a", "Grant", "repo:r/a.ts"),
            Action("action:m", "Model made", "repo:r/m.ts", Provenance.Model),
            new GraphNode(NodeKey.From("surface:s"), "Screen", Provenance.Deterministic),
            new GraphNode(NodeKey.From("flow:f"), "Flow", Provenance.Deterministic));

        var group = Assert.Single(SurfaceCandidates.Groups(found, []));

        Assert.Equal("action:a", Assert.Single(group.Candidates).NodeKey);
    }

    [Fact]
    public void A_key_a_person_decided_is_dropped_but_a_service_override_does_not_drop_it()
    {
        var found = Map(Action("action:a", "Grant", "repo:r/a.ts"), Action("action:b", "Revoke", "repo:r/b.ts"), Action("action:c", "Share", "repo:r/c.ts"));
        MapOverride[] overrides =
        [
            new(OverrideAction.Reject, NodeKey.From("action:a"), "joi", _now),
            new(OverrideAction.Reject, NodeKey.From("action:b"), MapOverride.JevWho, _now),
        ];

        var keys = SurfaceCandidates.Groups(found, overrides).SelectMany(g => g.Candidates).Select(c => c.NodeKey);

        Assert.Equal(["action:b", "action:c"], keys, StringComparer.Ordinal);
    }

    [Fact]
    public void A_candidate_has_the_raw_shape_with_sorted_evidence_and_edge_texts()
    {
        var node = Action("action:post", "Create account", "repo:r/z.ts", "repo:r/a.ts");
        var edges = new[]
        {
            new GraphEdge(NodeKey.From("action:post"), NodeKey.From("thing:account"), EdgeType.Writes),
            new GraphEdge(NodeKey.From("action:post"), NodeKey.From("surface:route/POST:/x"), EdgeType.On),
            new GraphEdge(NodeKey.From("code:z.ts"), NodeKey.From("action:post"), EdgeType.Implements),
        };

        var candidate = Assert.Single(Assert.Single(SurfaceCandidates.Groups(new ExtractedMap([node], edges), [])).Candidates);

        Assert.Equal("action", candidate.Kind);
        Assert.Equal("action", candidate.Pass);
        Assert.Equal("Create account", candidate.Name);
        Assert.Equal(["repo:r/a.ts", "repo:r/z.ts"], candidate.Evidence, StringComparer.Ordinal);
        Assert.Equal(["on surface:route/POST:/x", "writes thing:account"], candidate.Edges, StringComparer.Ordinal);
    }

    [Fact]
    public void The_same_found_map_gives_the_same_group_keys()
    {
        var found = Map(Action("action:b", "B", "repo:r/a.ts"), Action("action:a", "A", "repo:r/a.ts"));
        var reversed = Map(Action("action:a", "A", "repo:r/a.ts"), Action("action:b", "B", "repo:r/a.ts"));

        Assert.Equal(SurfaceCandidates.Groups(found, []).Select(g => g.Key), SurfaceCandidates.Groups(reversed, []).Select(g => g.Key), StringComparer.Ordinal);
    }

    private static GraphNode Action(string key, string label, string evidence, string? more = null, Provenance provenance = Provenance.Deterministic)
    {
        List<EvidenceRef> refs = [new(RecordKey.From(evidence))];
        if (more is not null)
        {
            refs.Add(new EvidenceRef(RecordKey.From(more)));
        }

        return new GraphNode(NodeKey.From(key), label, provenance, 0.8, refs);
    }

    private static GraphNode Action(string key, string label, string evidence, Provenance provenance) => Action(key, label, evidence, more: null, provenance);

    private static ExtractedMap Map(params GraphNode[] nodes) => new(nodes, []);
}
