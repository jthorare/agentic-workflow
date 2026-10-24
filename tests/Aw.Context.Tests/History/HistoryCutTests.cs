using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.History;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Trust = Aw.Context.Core.Results.Trust;

namespace Aw.Context.Tests.History;

public sealed class HistoryCutTests
{
    private static readonly DateTimeOffset _cut = new(2026, 7, 15, 0, 0, 0, TimeSpan.Zero);
    private static readonly NodeKey _anchor = NodeKey.From("feature:sharing");

    private static DateTimeOffset? None => null;
    private static LensQuote? NoQuote => null;

    private static DateTimeOffset Day(int month, int day) => new(2026, month, day, 0, 0, 0, TimeSpan.Zero);

    private static HistoryRecord Record(string name, DateTimeOffset? created, DateTimeOffset? updated, NodeKey? anchor = null) =>
        new(NodeKey.From("record:linear:issue/" + name), name, "linear", [_anchor], Trust.Untrusted, CitationState.Unchecked, [], [anchor ?? _anchor], created, updated, FetchedAt: null, Leaky: false);

    private static HistoryDecision Decision(string name, Provenance provenance, DateTimeOffset? at, string? quotedRecord = null, NodeKey[]? supersedes = null, NodeKey? anchor = null) =>
        new(NodeKey.From("decision:" + name), name, [_anchor], supersedes ?? [], Quotes: null, provenance, Confirmed: true, Trust.Trusted, at,
            quotedRecord is null ? NoQuote : new LensQuote(RecordKey.From("linear:issue/" + quotedRecord), "quote", Trust.Untrusted), [anchor ?? _anchor], Leaky: false);

    private static HistoryWindow Before() => new(Since: null, _cut);

    private static HistoryChange Change(string repo, string sha, DateTimeOffset at, NodeKey? anchor = null) =>
        new("commit:" + sha, ContextRepoName.From(repo), sha, at, "s", HistoryChangeKind.Commit, Branch: null, Pr: null, [], [anchor ?? _anchor], Trust.Untrusted);

    [Fact, Trait("Lane", "unit")]
    public void Record_created_after_before_is_dropped_and_counted()
    {
        var result = HistoryCut.Apply([Record("a", Day(7, 2), Day(7, 3)), Record("late", Day(7, 16), Day(7, 16))], [], Before());

        Assert.Equal(["record:linear:issue/a"], result.Records.Select(r => r.Key.Value), StringComparer.Ordinal);
        Assert.Equal((1, 1), (result.TotalRecords, result.AfterCut));
    }

    [Fact, Trait("Lane", "unit")]
    public void Record_edited_after_before_is_kept_leaky()
    {
        var result = HistoryCut.Apply([Record("edited", Day(7, 2), Day(7, 23)), Record("clean", Day(6, 1), Day(7, 6))], [], Before());

        Assert.Equal(["record:linear:issue/edited true", "record:linear:issue/clean false"], result.Records.Select(r => $"{r.Key.Value} {r.Leaky.ToString().ToLowerInvariant()}"), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Record_with_unknown_times_and_a_before_is_leaky()
    {
        var result = HistoryCut.Apply([Record("no-created", None, Day(7, 6)), Record("no-updated", Day(7, 1), None), Record("none", None, None)], [], Before());

        Assert.All(result.Records, r => Assert.True(r.Leaky));
        Assert.Equal(3, result.Records.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_before_never_drops_or_labels()
    {
        var result = HistoryCut.Apply([Record("late", Day(12, 1), Day(12, 2)), Record("none", None, None)], [Decision("d", Provenance.Human, Day(12, 1))], HistoryWindow.Open);

        Assert.Equal((2, 1, 0), (result.Records.Count, result.Decisions.Count, result.AfterCut));
        Assert.DoesNotContain(result.Records, r => r.Leaky);
        Assert.DoesNotContain(result.Decisions, d => d.Leaky);
    }

    [Fact, Trait("Lane", "unit")]
    public void Since_cuts_records_by_update_not_decisions()
    {
        var window = new HistoryWindow(Day(7, 1), Before: null);
        var result = HistoryCut.Apply(
            [Record("old", Day(1, 1), Day(2, 1)), Record("recent", Day(1, 1), Day(7, 5)), Record("unknown", None, None), Record("created-only", Day(7, 9), None)],
            [Decision("ancient", Provenance.Human, Day(1, 1))], window);

        Assert.Equal(["record:linear:issue/created-only", "record:linear:issue/recent", "record:linear:issue/unknown"], result.Records.Select(r => r.Key.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        _ = Assert.Single(result.Decisions);
        Assert.Equal(0, result.AfterCut);
    }

    [Fact, Trait("Lane", "unit")]
    public void Model_decision_follows_its_record()
    {
        var records = new[] { Record("late", Day(7, 16), Day(7, 16)), Record("edited", Day(7, 1), Day(7, 23)), Record("clean", Day(7, 1), Day(7, 2)) };
        var decisions = new[]
        {
            Decision("of-late", Provenance.Model, Day(12, 31), "late"),
            Decision("of-edited", Provenance.Deterministic, Day(12, 31), "edited"),
            Decision("of-clean", Provenance.Model, Day(12, 31), "clean"),
            Decision("of-unknown", Provenance.Model, Day(12, 31), "elsewhere"),
        };

        var result = HistoryCut.Apply(records, decisions, Before());

        Assert.Equal(["decision:of-clean false", "decision:of-edited true", "decision:of-unknown true"], result.Decisions.Select(d => $"{d.Node.Value} {d.Leaky.ToString().ToLowerInvariant()}").Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(2, result.AfterCut);   // the late record and its decision
    }

    [Fact, Trait("Lane", "unit")]
    public void Human_decision_is_cut_by_at_and_null_at_is_kept_as_leaky()
    {
        var result = HistoryCut.Apply([], [Decision("early", Provenance.Human, Day(3, 18)), Decision("late", Provenance.Human, Day(8, 1)), Decision("undated", Provenance.Human, None)], Before());

        Assert.Equal(["decision:early", "decision:undated"], result.Decisions.Select(d => d.Node.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(["decision:undated"], result.Decisions.Where(d => d.Leaky).Select(d => d.Node.Value), StringComparer.Ordinal);   // an unknown time can't vouch for the cut, as with a record
        Assert.Equal(1, result.AfterCut);
    }

    [Fact, Trait("Lane", "unit")]
    public void Superseded_edge_to_a_dropped_decision_is_removed()
    {
        var late = NodeKey.From("decision:late");
        var early = NodeKey.From("decision:early");
        var result = HistoryCut.Apply([], [Decision("early", Provenance.Human, Day(3, 18), supersedes: [late]), Decision("late", Provenance.Human, Day(8, 1), supersedes: [early])], Before());

        Assert.Equal(early.Value, Assert.Single(result.Decisions).Node.Value);
        Assert.Empty(result.Decisions[0].Supersedes);
    }

    [Fact, Trait("Lane", "unit")]
    public void Records_order_by_update_desc_then_key()
    {
        var result = HistoryCut.Apply([Record("b", Day(1, 1), Day(2, 1)), Record("a", Day(1, 1), Day(2, 1)), Record("c", Day(1, 1), Day(3, 1)), Record("n", Day(1, 1), None)], [], HistoryWindow.Open);

        Assert.Equal(["c", "a", "b", "n"], result.Records.Select(r => r.Label), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Changes_order_by_at_desc_then_repo_then_sha()
    {
        var list = HistoryCut.Changes([Change("zeta", "bb", Day(1, 1)), Change("alpha", "cc", Day(1, 1)), Change("alpha", "aa", Day(1, 1)), Change("alpha", "dd", Day(2, 1))]);

        Assert.Equal(["dd", "aa", "cc", "bb"], list.Items.Select(c => c.Sha), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Lists_are_not_cut_at_100()
    {
        var records = Enumerable.Range(0, 150).Select(i => Record("r" + i.ToString("D3", System.Globalization.CultureInfo.InvariantCulture), Day(1, 1), Day(2, 1))).ToList();
        var changes = Enumerable.Range(0, 120).Select(i => Change("alpha", i.ToString("x2", System.Globalization.CultureInfo.InvariantCulture), Day(1, 1))).ToList();

        var result = HistoryCut.Apply(records, [], HistoryWindow.Open);
        var list = HistoryCut.Changes(changes);

        Assert.Equal((150, 150), (result.Records.Count, result.TotalRecords));
        Assert.False(result.RecordsTruncated);
        Assert.Equal((120, 120), (list.Items.Count, list.Total));
        Assert.False(list.Truncated);
    }

    [Fact, Trait("Lane", "unit")]
    public void Two_anchors_one_entry_with_anchors_listed()
    {
        var other = NodeKey.From("feature:tabs");
        var result = HistoryCut.Apply(
            [Record("a", Day(1, 1), Day(2, 1)), Record("a", Day(1, 1), Day(2, 1), other)],
            [Decision("d", Provenance.Human, Day(1, 1)), Decision("d", Provenance.Human, Day(1, 1), anchor: other)], HistoryWindow.Open);
        var changes = HistoryCut.Changes([Change("alpha", "aa", Day(1, 1)), Change("alpha", "aa", Day(1, 1), other)]);

        Assert.Equal(["feature:sharing", "feature:tabs"], Assert.Single(result.Records).Anchors.Select(a => a.Value), StringComparer.Ordinal);
        Assert.Equal(["feature:sharing", "feature:tabs"], Assert.Single(result.Decisions).Anchors.Select(a => a.Value), StringComparer.Ordinal);
        Assert.Equal(["feature:sharing", "feature:tabs"], Assert.Single(changes.Items).Anchors.Select(a => a.Value), StringComparer.Ordinal);
    }
}
