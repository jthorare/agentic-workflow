using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;

namespace Aw.Context.Tests.Map;

public sealed class MapKeysTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("feature:layout.split-pane", NodeKind.Feature)]
    [InlineData("surface:command/split-vertical", NodeKind.Surface)]
    [InlineData("code:src/layout/SplitPane.ts#setOrientation", NodeKind.Code)]
    [InlineData("record:linear:issue/0e5a1b00-0000-4000-8000-000000000103", NodeKind.Record)]
    [InlineData("concept:scheduling", NodeKind.Concept)]
    public void A_key_names_its_kind_by_its_scheme(string key, NodeKind kind) => Assert.Equal(kind, NodeKey.From(key).Kind);

    [Theory, Trait("Lane", "unit")]
    [InlineData("widget:x")]
    [InlineData("Feature:x")]
    [InlineData("feature:")]
    [InlineData("feature: leading-space")]
    [InlineData("feature:bad\u0001")]
    public void A_key_with_an_unknown_kind_or_no_name_is_refused(string key) => Assert.False(NodeKey.TryFrom(key, out _));

    // Golden snapshot of EntryKey version 1: a change here is a key-version bump with a migration (spec §6.1).
    [Theory, Trait("Lane", "unit")]
    [InlineData("feature:Layout.Split-Pane", "feature:layout.split-pane")]
    [InlineData("action:Tab-Manager.Restore-Layout  ", "action:tab-manager.restore-layout")]
    [InlineData("thing:Pane-Layout", "thing:pane-layout")]
    [InlineData("flow:Open-Tab", "flow:open-tab")]
    [InlineData("surface:setting/paneResizeStep", "surface:setting/paneResizeStep")]
    [InlineData("code:src/layout/SplitPane.ts", "code:src/layout/SplitPane.ts")]
    [InlineData("feature:café", "feature:café")]
    [InlineData("record:linear:issue/ABC", "record:linear:issue/ABC")]
    [InlineData("concept:Self-Scheduling", "concept:self-scheduling")]
    public void EntryKey_v1_golden_snapshot(string written, string key)
    {
        Assert.Equal(1, EntryKey.Version);
        Assert.Equal(key, EntryKey.For(NodeKey.From(written)).Value);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(EdgeType.PartOf, NodeKind.Surface, NodeKind.Feature, true)]
    [InlineData(EdgeType.PartOf, NodeKind.Feature, NodeKind.Surface, false)]
    [InlineData(EdgeType.Reads, NodeKind.Action, NodeKind.Thing, true)]
    [InlineData(EdgeType.Reads, NodeKind.Feature, NodeKind.Thing, false)]
    [InlineData(EdgeType.Implements, NodeKind.Code, NodeKind.Action, true)]
    [InlineData(EdgeType.Implements, NodeKind.Code, NodeKind.Thing, false)]
    [InlineData(EdgeType.Decides, NodeKind.Decision, NodeKind.Thing, true)]
    [InlineData(EdgeType.Quotes, NodeKind.Decision, NodeKind.Feature, false)]
    public void The_edge_table_decides_which_kinds_an_edge_joins(EdgeType type, NodeKind from, NodeKind to, bool allowed) =>
        Assert.Equal(allowed, EdgeRules.Allows(type, from, to));

    [Theory, Trait("Lane", "unit")]
    [InlineData(Provenance.Deterministic, false, Trust.Trusted)]
    [InlineData(Provenance.Human, false, Trust.Trusted)]
    [InlineData(Provenance.Model, false, Trust.Untrusted)]
    [InlineData(Provenance.Model, true, Trust.Trusted)]
    public void Trust_follows_provenance_and_acceptance(Provenance provenance, bool accepted, Trust trust) =>
        Assert.Equal(trust, TrustPolicy.For(provenance, accepted));

    [Fact, Trait("Lane", "unit")]
    public void A_decision_key_hashes_its_record_and_quote_not_its_wording()
    {
        var record = RecordKey.From("linear:issue/0e5a1b00-0000-4000-8000-000000000103");
        var first = new DecisionRow(record, "keeps the focused pane first", "Focus stays first.");
        var reworded = first with { Wording = "The first pane keeps focus." };

        Assert.Equal(first.ToKey(), reworded.ToKey());
        Assert.NotEqual(first.ToKey(), (first with { Quote = "keeps the focused pane last" }).ToKey());
        Assert.Matches("^decision:[0-9a-f]{64}$", first.ToKey().Value);
    }
}
