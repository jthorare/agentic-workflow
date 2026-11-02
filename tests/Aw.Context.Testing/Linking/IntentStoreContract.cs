using Aw.Context.Core;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Map;
using Xunit;

namespace Aw.Context.Testing.Linking;

/// <summary>The intent store's contract (spec §6.2, S-5B): the fake and the SQLite store pass the same tests.</summary>
public abstract class IntentStoreContract
{
    private static readonly SourceId _linear = SourceId.From("linear");
    private static readonly RecordKey _r103 = RecordKey.From("linear:issue/0e5a1b00-0000-4000-8000-000000000103");
    private static readonly RecordKey _r104 = RecordKey.From("linear:issue/0e5a1b00-0000-4000-8000-000000000104");
    private static readonly DateTimeOffset _at = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken None => TestContext.Current.CancellationToken;

    /// <summary>Creates a fresh, empty store.</summary>
    /// <returns>The store under test.</returns>
    protected abstract IIntentStore CreateStore();

    private static IntentRecord Record(RecordKey key, string version, string alias) => new(key, _linear, version, "Title of " + alias, [alias]);

    private static IntentLink Link(RecordKey record, string target, LinkRule rule = LinkRule.IssueKey) => new(record, NodeKey.From(target), rule, 1.0, "ab12");

    private static IntentPage Page(string? cursor, IReadOnlyList<IntentRecord> records, IReadOnlyList<IntentLink> links) => new(_linear, records, links, cursor, _at);

    /// <summary>An empty store has an empty layer and a zero state.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_empty_store_has_an_empty_layer()
    {
        const string test = nameof(An_empty_store_has_an_empty_layer);
        var store = CreateStore();

        var layer = Expect.Ok(await store.Layer(None), test);
        var state = Expect.Ok(await store.State(_linear, None), test);

        Expect.That(layer.Records.Count == 0 && layer.Links.Count == 0 && layer.Decisions.Count == 0, test, "rows in an empty store");
        Expect.That(state == new IntentSourceState(Cursor: null, LastSync: null, LastError: null, Records: 0, Links: 0, Decisions: 0, Queued: 0), test, $"state {state}");
    }

    /// <summary>A committed page reads back as written, with its cursor and counts.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_committed_page_reads_back_and_moves_the_cursor()
    {
        const string test = nameof(A_committed_page_reads_back_and_moves_the_cursor);
        var store = CreateStore();
        var record = Record(_r103, "v1", "SPL-103");
        var link = Link(_r103, "feature:layout.split-pane.orientation");

        var commit = Expect.Ok(await store.CommitPage(Page("{\"nodeOffset\":4}", [record], [link]), None), test);
        var layer = Expect.Ok(await store.Layer(None), test);
        var state = Expect.Ok(await store.State(_linear, None), test);

        Expect.That(commit == new IntentCommit(1, 1, 1), test, $"commit {commit}");
        Expect.That(layer.Records.Count == 1 && layer.Records[0] is var got && got.Key == record.Key && got.Version == record.Version && got.Title == record.Title && got.Aliases.SequenceEqual(record.Aliases, StringComparer.Ordinal) && layer.Links.SequenceEqual([link]), test, "records or links read back differently");
        Expect.That(state.Cursor == "{\"nodeOffset\":4}" && state.LastSync == _at && state.LastError is null && state is { Records: 1, Links: 1, Queued: 1 }, test, $"state {state}");
    }

    /// <summary>The creation instant a source gave reads back; a record without one reads back null.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Created_at_round_trips()
    {
        const string test = nameof(Created_at_round_trips);
        var store = CreateStore();
        var created = new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.Zero);

        _ = Expect.Ok(await store.CommitPage(Page("c1", [Record(_r103, "v1", "SPL-103") with { CreatedAt = created }, Record(_r104, "v1", "SPL-104")], []), None), test);
        var layer = Expect.Ok(await store.Layer(None), test);

        Expect.That(layer.Records.Single(r => r.Key.Value == _r103.Value).CreatedAt == created, test, "the creation instant didn't read back");
        Expect.That(layer.Records.Single(r => r.Key.Value == _r104.Value).CreatedAt is null, test, "a record with no creation instant read one back");
    }

    /// <summary>The instant the page was fetched reads back on the record; a replay of the same version keeps the first.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Fetched_at_reads_back_as_the_pages_instant_and_a_replay_keeps_it()
    {
        const string test = nameof(Fetched_at_reads_back_as_the_pages_instant_and_a_replay_keeps_it);
        var store = CreateStore();
        _ = Expect.Ok(await store.CommitPage(Page("c1", [Record(_r103, "v1", "SPL-103")], []), None), test);

        var layer = Expect.Ok(await store.Layer(None), test);

        Expect.That(layer.Records.Single().FetchedAt == _at, test, "the fetch instant didn't read back");
    }

    /// <summary>Replaying the same version keeps the stored row, creation instant included.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Replay_of_the_same_version_writes_nothing()
    {
        const string test = nameof(Replay_of_the_same_version_writes_nothing);
        var store = CreateStore();
        var first = new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.Zero);
        _ = Expect.Ok(await store.CommitPage(Page("c1", [Record(_r103, "v1", "SPL-103") with { CreatedAt = first }], []), None), test);

        var replay = Expect.Ok(await store.CommitPage(Page("c2", [Record(_r103, "v1", "SPL-103") with { CreatedAt = first.AddDays(1) }], []), None), test);
        var layer = Expect.Ok(await store.Layer(None), test);

        Expect.That(replay.Changed == 0, test, "changed " + replay.Changed.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Expect.That(layer.Records.Single().CreatedAt == first, test, "the replay rewrote the creation instant");
    }

    /// <summary>A version that moves rewrites the creation instant with the new fetch.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_version_move_rewrites_created_at()
    {
        const string test = nameof(A_version_move_rewrites_created_at);
        var store = CreateStore();
        var created = new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.Zero);
        _ = Expect.Ok(await store.CommitPage(Page("c1", [Record(_r103, "v1", "SPL-103")], []), None), test);

        _ = Expect.Ok(await store.CommitPage(Page("c2", [Record(_r103, "v2", "SPL-103") with { CreatedAt = created }], []), None), test);
        var layer = Expect.Ok(await store.Layer(None), test);

        Expect.That(layer.Records.Single().CreatedAt == created, test, "the version move didn't store the creation instant");
    }

    /// <summary>Replaying a page writes no new rows and reports nothing changed.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Replaying_a_page_writes_no_new_rows()
    {
        const string test = nameof(Replaying_a_page_writes_no_new_rows);
        var store = CreateStore();
        var page = Page("c1", [Record(_r103, "v1", "SPL-103"), Record(_r104, "v1", "SPL-104")], [Link(_r103, "feature:a"), Link(_r104, "feature:b", LinkRule.Path)]);
        _ = Expect.Ok(await store.CommitPage(page, None), test);
        var before = Expect.Ok(await store.Layer(None), test);

        var replay = Expect.Ok(await store.CommitPage(page, None), test);
        var after = Expect.Ok(await store.Layer(None), test);

        Expect.That(replay.Changed == 0, test, "replay changed " + replay.Changed.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Expect.That(before.Records.Count == after.Records.Count && before.Links.Count == after.Links.Count && before.Links.SequenceEqual(after.Links), test, "the replay wrote rows");
    }

    /// <summary>A new version of a record replaces its links; another record's links stay.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_new_version_replaces_that_records_links_only()
    {
        const string test = nameof(A_new_version_replaces_that_records_links_only);
        var store = CreateStore();
        _ = Expect.Ok(await store.CommitPage(Page("c1", [Record(_r103, "v1", "SPL-103"), Record(_r104, "v1", "SPL-104")], [Link(_r103, "feature:old"), Link(_r104, "feature:b")]), None), test);

        var next = Expect.Ok(await store.CommitPage(Page("c2", [Record(_r103, "v2", "SPL-103")], [Link(_r103, "feature:new")]), None), test);
        var layer = Expect.Ok(await store.Layer(None), test);

        Expect.That(next.Changed == 1, test, "changed " + next.Changed.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Expect.That(layer.Links.Select(l => l.Target.Value).Order(StringComparer.Ordinal).SequenceEqual(["feature:b", "feature:new"], StringComparer.Ordinal), test, "links: " + string.Join(',', layer.Links.Select(l => l.Target.Value)));
        Expect.That(layer.Records.Single(r => r.Key.Value == _r103.Value).Version == "v2", test, "the version didn't move");
    }

    /// <summary>A page that fails changes nothing, cursor included (the links and the cursor commit together).</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_failing_page_leaves_rows_and_cursor_unchanged()
    {
        const string test = nameof(A_failing_page_leaves_rows_and_cursor_unchanged);
        var store = CreateStore();
        _ = Expect.Ok(await store.CommitPage(Page("c1", [Record(_r103, "v1", "SPL-103")], [Link(_r103, "feature:a")]), None), test);

        var broken = await store.CommitPage(Page("c2", [Record(_r104, "v1", "SPL-104")], [Link(_r103, "feature:z"), Link(RecordKey.From("linear:issue/unknown"), "feature:y")]), None);
        var layer = Expect.Ok(await store.Layer(None), test);
        var state = Expect.Ok(await store.State(_linear, None), test);

        Expect.That(broken.IsError && broken.Error.Code == ContextErrors.StoreUnavailable.Code, test, "a link to an unknown record was accepted");
        Expect.That(state.Cursor == "c1" && layer.Records.Count == 1 && layer.Links.Count == 1, test, $"state {state}, {layer.Records.Count} records");
    }

    /// <summary>A failed run is remembered with its code and keeps the cursor and last sync.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_failed_run_keeps_the_cursor_and_records_its_code()
    {
        const string test = nameof(A_failed_run_keeps_the_cursor_and_records_its_code);
        var store = CreateStore();
        _ = Expect.Ok(await store.CommitPage(Page("c1", [Record(_r103, "v1", "SPL-103")], []), None), test);

        _ = Expect.Ok(await store.RecordFailure(_linear, "SND-CONTEXT-010", None), test);
        var state = Expect.Ok(await store.State(_linear, None), test);

        Expect.That(state.Cursor == "c1" && state.LastSync == _at && state.LastError == "SND-CONTEXT-010", test, $"state {state}");
    }

    private static (DecisionRow Decision, GraphEdge Decides) Decided(RecordKey record, string quote, string node)
    {
        var decision = new DecisionRow(record, quote, "Wording of " + quote, Provenance.Model, _at);
        return (decision, new GraphEdge(decision.ToKey(), NodeKey.From(node), EdgeType.Decides, Provenance.Model, 0.8, At: _at));
    }

    /// <summary>A record is queued from its first commit until its decisions are saved; the saved decisions and edges read back.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_new_record_is_queued_until_its_decisions_are_saved()
    {
        const string test = nameof(A_new_record_is_queued_until_its_decisions_are_saved);
        var store = CreateStore();
        _ = Expect.Ok(await store.CommitPage(Page("c1", [Record(_r103, "v1", "SPL-103")], [Link(_r103, "feature:a")]), None), test);
        var queued = Expect.Ok(await store.Queued(_linear, None), test);
        var (decision, decides) = Decided(_r103, "keeps the focused pane first", "feature:a");

        _ = Expect.Ok(await store.SaveDecisions(new DecisionBatch(_r103, "v1", [decision], [decides]), None), test);
        var after = Expect.Ok(await store.Queued(_linear, None), test);
        var layer = Expect.Ok(await store.Layer(None), test);
        var state = Expect.Ok(await store.State(_linear, None), test);

        Expect.That(queued.Select(k => k.Value).SequenceEqual([_r103.Value], StringComparer.Ordinal) && after.Count == 0, test, $"queued {queued.Count} then {after.Count}");
        Expect.That(layer.Decisions.Count == 1 && layer.Decisions[0].Quote == decision.Quote && layer.Decisions[0].Provenance == Provenance.Model, test, "the decision didn't read back");
        Expect.That(layer.DecisionEdges.Count == 1 && layer.DecisionEdges[0].To.Value == "feature:a" && layer.DecisionEdges[0].Type == EdgeType.Decides, test, "the decides edge didn't read back");
        Expect.That(state.Decisions == 1 && state.Queued == 0, test, $"state {state}");
    }

    /// <summary>Decisions extracted from an older version of a record are not saved.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Saving_decisions_for_a_stale_version_changes_nothing()
    {
        const string test = nameof(Saving_decisions_for_a_stale_version_changes_nothing);
        var store = CreateStore();
        _ = Expect.Ok(await store.CommitPage(Page("c1", [Record(_r103, "v1", "SPL-103")], [Link(_r103, "feature:a")]), None), test);
        _ = Expect.Ok(await store.CommitPage(Page("c2", [Record(_r103, "v2", "SPL-103")], [Link(_r103, "feature:a")]), None), test);
        var (decision, decides) = Decided(_r103, "old wording", "feature:a");

        _ = Expect.Ok(await store.SaveDecisions(new DecisionBatch(_r103, "v1", [decision], [decides]), None), test);

        var layer = Expect.Ok(await store.Layer(None), test);
        var queued = Expect.Ok(await store.Queued(_linear, None), test);
        Expect.That(layer.Decisions.Count == 0 && layer.DecisionEdges.Count == 0 && queued.Count == 1, test, $"{layer.Decisions.Count} decisions, {queued.Count} queued");
    }

    /// <summary>A new version queues the record again, and saving replaces the old version's decisions and edges.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_new_version_queues_the_record_again_and_saving_replaces_its_decisions()
    {
        const string test = nameof(A_new_version_queues_the_record_again_and_saving_replaces_its_decisions);
        var store = CreateStore();
        _ = Expect.Ok(await store.CommitPage(Page("c1", [Record(_r103, "v1", "SPL-103")], [Link(_r103, "feature:a")]), None), test);
        var (first, firstEdge) = Decided(_r103, "first", "feature:a");
        _ = Expect.Ok(await store.SaveDecisions(new DecisionBatch(_r103, "v1", [first], [firstEdge]), None), test);

        _ = Expect.Ok(await store.CommitPage(Page("c2", [Record(_r103, "v2", "SPL-103")], [Link(_r103, "feature:a")]), None), test);
        var requeued = Expect.Ok(await store.Queued(_linear, None), test);
        var (second, secondEdge) = Decided(_r103, "second", "feature:a");
        _ = Expect.Ok(await store.SaveDecisions(new DecisionBatch(_r103, "v2", [second], [secondEdge]), None), test);

        var layer = Expect.Ok(await store.Layer(None), test);
        Expect.That(requeued.Count == 1, test, "the new version wasn't queued");
        Expect.That(layer.Decisions.Select(d => d.Quote).SequenceEqual(["second"], StringComparer.Ordinal) && layer.DecisionEdges.Select(e => e.From.Value).SequenceEqual([second.ToKey().Value], StringComparer.Ordinal), test, "the old decision stayed");
    }

    /// <summary>A checked pair is stored once, smaller key first, and shows in the layer.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_conflict_is_stored_once_per_pair_and_read_back_in_the_layer()
    {
        const string test = nameof(A_conflict_is_stored_once_per_pair_and_read_back_in_the_layer);
        var store = CreateStore();
        var a = NodeKey.From("decision:" + new string('a', 64));
        var b = NodeKey.From("decision:" + new string('b', 64));

        _ = Expect.Ok(await store.SaveConflict(DecisionConflict.Of(b, a, ConflictVerdict.Contradicts, _at), None), test);
        _ = Expect.Ok(await store.SaveConflict(DecisionConflict.Of(a, b, ConflictVerdict.Agrees, _at), None), test);

        var layer = Expect.Ok(await store.Layer(None), test);
        Expect.That(layer.Conflicts.Count == 1, test, $"{layer.Conflicts.Count} conflicts");
        var only = layer.Conflicts[0];
        Expect.That(only.A.Value == a.Value && only.B.Value == b.Value && only.Verdict == ConflictVerdict.Contradicts, test, $"{only}");
    }

    /// <summary>A pending save keeps the record queued, so a stopped conflict check is retried by the next sync.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_pending_save_stores_the_decisions_but_keeps_the_record_queued()
    {
        const string test = nameof(A_pending_save_stores_the_decisions_but_keeps_the_record_queued);
        var store = CreateStore();
        _ = Expect.Ok(await store.CommitPage(Page("c1", [Record(_r103, "v1", "SPL-103")], [Link(_r103, "feature:a")]), None), test);
        var (decision, decides) = Decided(_r103, "first", "feature:a");

        _ = Expect.Ok(await store.SaveDecisions(new DecisionBatch(_r103, "v1", [decision], [decides]) { Pending = true }, None), test);
        var pending = Expect.Ok(await store.Queued(_linear, None), test);
        var layer = Expect.Ok(await store.Layer(None), test);
        _ = Expect.Ok(await store.SaveDecisions(new DecisionBatch(_r103, "v1", [decision], [decides]), None), test);
        var done = Expect.Ok(await store.Queued(_linear, None), test);

        Expect.That(pending.Count == 1 && layer.Decisions.Count == 1, test, $"{pending.Count} queued, {layer.Decisions.Count} decisions after a pending save");
        Expect.That(done.Count == 0, test, "the final save didn't mark the record extracted");
    }

    /// <summary>Saving a record's decisions again keeps the verdicts of pairs whose decision keys survive and drops the ones that went away.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Saving_again_keeps_the_verdicts_of_decisions_whose_keys_survive()
    {
        const string test = nameof(Saving_again_keeps_the_verdicts_of_decisions_whose_keys_survive);
        var store = CreateStore();
        _ = Expect.Ok(await store.CommitPage(Page("c1", [Record(_r103, "v1", "SPL-103"), Record(_r104, "v1", "SPL-104")], [Link(_r103, "feature:a"), Link(_r104, "feature:a")]), None), test);
        var (kept, keptEdge) = Decided(_r103, "kept", "feature:a");
        var (other, otherEdge) = Decided(_r104, "other", "feature:a");
        var (replacement, replacementEdge) = Decided(_r103, "replacement", "feature:a");
        _ = Expect.Ok(await store.SaveDecisions(new DecisionBatch(_r103, "v1", [kept], [keptEdge]), None), test);
        _ = Expect.Ok(await store.SaveDecisions(new DecisionBatch(_r104, "v1", [other], [otherEdge]), None), test);
        _ = Expect.Ok(await store.SaveConflict(DecisionConflict.Of(kept.ToKey(), other.ToKey(), ConflictVerdict.Contradicts, _at), None), test);

        _ = Expect.Ok(await store.SaveDecisions(new DecisionBatch(_r103, "v1", [kept, replacement], [keptEdge, replacementEdge]), None), test);
        var same = Expect.Ok(await store.Layer(None), test);
        _ = Expect.Ok(await store.SaveDecisions(new DecisionBatch(_r103, "v1", [replacement], [replacementEdge]), None), test);
        var gone = Expect.Ok(await store.Layer(None), test);

        Expect.That(same.Conflicts.Count == 1 && same.Conflicts[0].Verdict == ConflictVerdict.Contradicts, test, $"{same.Conflicts.Count} verdicts after a re-save with the same key");
        Expect.That(gone.Conflicts.Count == 0 && gone.Decisions.Count == 2, test, $"{gone.Conflicts.Count} verdicts and {gone.Decisions.Count} decisions after the key went away");
    }

    /// <summary>A pending save queues a record that was already extracted (a map change re-extracts it), so its stopped conflict check is retried.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_pending_save_queues_a_done_record_again()
    {
        const string test = nameof(A_pending_save_queues_a_done_record_again);
        var store = CreateStore();
        _ = Expect.Ok(await store.CommitPage(Page("c1", [Record(_r103, "v1", "SPL-103")], [Link(_r103, "feature:a")]), None), test);
        var (decision, decides) = Decided(_r103, "first", "feature:a");
        _ = Expect.Ok(await store.SaveDecisions(new DecisionBatch(_r103, "v1", [decision], [decides]), None), test);
        var done = Expect.Ok(await store.Queued(_linear, None), test);

        _ = Expect.Ok(await store.SaveDecisions(new DecisionBatch(_r103, "v1", [decision], [decides]) { Pending = true }, None), test);
        var again = Expect.Ok(await store.Queued(_linear, None), test);
        var state = Expect.Ok(await store.State(_linear, None), test);

        Expect.That(done.Count == 0 && again.Select(k => k.Value).SequenceEqual([_r103.Value], StringComparer.Ordinal) && state.Queued == 1, test, $"{done.Count} queued when done, {again.Count} after a pending save, state {state}");
    }

    /// <summary>A record is linked under the map version of the page that committed it; a later page under another version moves it.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task LinkedUnder_lists_the_records_whose_page_carried_that_map_version()
    {
        const string test = nameof(LinkedUnder_lists_the_records_whose_page_carried_that_map_version);
        var store = CreateStore();
        _ = Expect.Ok(await store.CommitPage(Page("c1", [Record(_r103, "v1", "SPL-103")], [Link(_r103, "feature:a")]) with { MapVersion = 1 }, None), test);
        var underOne = Expect.Ok(await store.LinkedUnder(_linear, 1, None), test);
        var underTwoBefore = Expect.Ok(await store.LinkedUnder(_linear, 2, None), test);

        _ = Expect.Ok(await store.CommitPage(Page("c2", [Record(_r103, "v1", "SPL-103")], [Link(_r103, "feature:a")]) with { MapVersion = 2 }, None), test);
        var underOneAfter = Expect.Ok(await store.LinkedUnder(_linear, 1, None), test);
        var underTwo = Expect.Ok(await store.LinkedUnder(_linear, 2, None), test);

        Expect.That(underOne.Count == 1 && underTwoBefore.Count == 0 && underOneAfter.Count == 0 && underTwo.Select(k => k.Value).SequenceEqual([_r103.Value], StringComparer.Ordinal), test,
            $"{underOne.Count}/{underTwoBefore.Count} before, {underOneAfter.Count}/{underTwo.Count} after the second page");
    }

    /// <summary>Replacing one rule's links swaps that rule's links only: other rules' links, the map version the record was linked under and its extraction state stay.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task ReplaceRuleLinks_swaps_one_rules_links_and_leaves_the_rest_alone()
    {
        const string test = nameof(ReplaceRuleLinks_swaps_one_rules_links_and_leaves_the_rest_alone);
        var store = CreateStore();
        var record = Record(_r103, "v1", "SPL-103");
        var key = Link(_r103, "feature:a");
        var oldAlias = Link(_r103, "concept:old", LinkRule.Alias);
        var newAlias = Link(_r103, "concept:new", LinkRule.Alias);
        _ = Expect.Ok(await store.CommitPage(Page("c1", [record], [key, oldAlias]) with { MapVersion = 1 }, None), test);
        var (decision, decides) = Decided(_r103, "first", "feature:a");
        _ = Expect.Ok(await store.SaveDecisions(new DecisionBatch(_r103, "v1", [decision], [decides]), None), test);

        _ = Expect.Ok(await store.ReplaceRuleLinks(_r103, LinkRule.Alias, [newAlias], None), test);
        var layer = Expect.Ok(await store.Layer(None), test);
        var under = Expect.Ok(await store.LinkedUnder(_linear, 1, None), test);
        var queued = Expect.Ok(await store.Queued(_linear, None), test);

        Expect.That(layer.Links.Select(l => (l.Target.Value, l.Rule)).SequenceEqual([("concept:new", LinkRule.Alias), ("feature:a", LinkRule.IssueKey)]), test, $"links {string.Join(", ", layer.Links.Select(l => l.Target.Value + "/" + l.Rule))}");
        Expect.That(under.Select(k => k.Value).SequenceEqual([_r103.Value], StringComparer.Ordinal), test, $"{under.Count} linked under the old map version");
        Expect.That(queued.Count == 0 && layer.Decisions.Count == 1, test, $"{queued.Count} queued, {layer.Decisions.Count} decisions kept");
    }

    /// <summary>An empty list removes the rule's links; another record's links of that rule stay.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task ReplaceRuleLinks_with_none_removes_that_rules_links_of_that_record_only()
    {
        const string test = nameof(ReplaceRuleLinks_with_none_removes_that_rules_links_of_that_record_only);
        var store = CreateStore();
        _ = Expect.Ok(await store.CommitPage(Page("c1", [Record(_r103, "v1", "SPL-103"), Record(_r104, "v1", "SPL-104")], [Link(_r103, "concept:a", LinkRule.Alias), Link(_r104, "concept:a", LinkRule.Alias)]), None), test);

        _ = Expect.Ok(await store.ReplaceRuleLinks(_r103, LinkRule.Alias, [], None), test);
        var layer = Expect.Ok(await store.Layer(None), test);

        Expect.That(layer.Links.Count == 1 && layer.Links[0].Record == _r104, test, $"{layer.Links.Count} links left");
    }

    /// <summary>Requeue queues a done record again and leaves its decisions and edges alone; a version that moved on is ignored.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Requeue_queues_a_done_record_again_and_keeps_its_decisions()
    {
        const string test = nameof(Requeue_queues_a_done_record_again_and_keeps_its_decisions);
        var store = CreateStore();
        _ = Expect.Ok(await store.CommitPage(Page("c1", [Record(_r103, "v1", "SPL-103")], [Link(_r103, "feature:a")]), None), test);
        var (decision, decides) = Decided(_r103, "first", "feature:a");
        _ = Expect.Ok(await store.SaveDecisions(new DecisionBatch(_r103, "v1", [decision], [decides]), None), test);

        _ = Expect.Ok(await store.Requeue(_r103, "v0", None), test);
        var stale = Expect.Ok(await store.Queued(_linear, None), test);
        _ = Expect.Ok(await store.Requeue(_r103, "v1", None), test);
        var again = Expect.Ok(await store.Queued(_linear, None), test);
        var layer = Expect.Ok(await store.Layer(None), test);

        Expect.That(stale.Count == 0 && again.Select(k => k.Value).SequenceEqual([_r103.Value], StringComparer.Ordinal) && layer.Decisions.Count == 1 && layer.DecisionEdges.Count == 1, test,
            $"{stale.Count} queued after a stale requeue, {again.Count} after a requeue, {layer.Decisions.Count} decisions, {layer.DecisionEdges.Count} edges");
    }

    /// <summary>A done record whose page moves its map version is queued in the same commit (only a finished extraction marks it done again) and keeps its decisions; the same map version again changes nothing.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_page_that_moves_a_done_records_map_version_queues_it_until_extracted_again()
    {
        const string test = nameof(A_page_that_moves_a_done_records_map_version_queues_it_until_extracted_again);
        var store = CreateStore();
        var record = Record(_r103, "v1", "SPL-103");
        _ = Expect.Ok(await store.CommitPage(Page("c1", [record], [Link(_r103, "feature:a")]) with { MapVersion = 1 }, None), test);
        var (decision, decides) = Decided(_r103, "first", "feature:a");
        _ = Expect.Ok(await store.SaveDecisions(new DecisionBatch(_r103, "v1", [decision], [decides]), None), test);
        var done = Expect.Ok(await store.Queued(_linear, None), test);

        _ = Expect.Ok(await store.CommitPage(Page("c2", [record], [Link(_r103, "feature:a")]) with { MapVersion = 1 }, None), test);
        var sameMap = Expect.Ok(await store.Queued(_linear, None), test);
        _ = Expect.Ok(await store.CommitPage(Page("c3", [record], [Link(_r103, "feature:a")]) with { MapVersion = 2 }, None), test);
        var newMap = Expect.Ok(await store.Queued(_linear, None), test);
        var layer = Expect.Ok(await store.Layer(None), test);
        _ = Expect.Ok(await store.SaveDecisions(new DecisionBatch(_r103, "v1", [decision], [decides]), None), test);
        var extracted = Expect.Ok(await store.Queued(_linear, None), test);

        Expect.That(done.Count == 0 && sameMap.Count == 0, test, $"{done.Count} queued when done, {sameMap.Count} after the same map version again");
        Expect.That(newMap.Select(k => k.Value).SequenceEqual([_r103.Value], StringComparer.Ordinal) && layer.Decisions.Count == 1 && extracted.Count == 0, test,
            $"{newMap.Count} queued after a new map version, {layer.Decisions.Count} decisions kept, {extracted.Count} queued once extracted again");
    }

    /// <summary>A human decision is never replaced by a machine one with the same key, and keeps its edges.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_human_decision_is_never_replaced_by_a_machine_one()
    {
        const string test = nameof(A_human_decision_is_never_replaced_by_a_machine_one);
        var store = CreateStore();
        _ = Expect.Ok(await store.CommitPage(Page("c1", [Record(_r103, "v1", "SPL-103")], [Link(_r103, "feature:a")]), None), test);
        var human = new DecisionRow(_r103, "kept by a person", "The owner's wording", Provenance.Human, _at);
        var humanEdge = new GraphEdge(human.ToKey(), NodeKey.From("feature:a"), EdgeType.Decides, Provenance.Human, 1.0, At: _at);
        _ = Expect.Ok(await store.SaveDecisions(new DecisionBatch(_r103, "v1", [human], [humanEdge]), None), test);
        var machine = human with { Wording = "The model's wording", Provenance = Provenance.Model };
        var machineEdge = humanEdge with { Provenance = Provenance.Model, Confidence = 0.5 };

        _ = Expect.Ok(await store.SaveDecisions(new DecisionBatch(_r103, "v1", [machine], [machineEdge]), None), test);
        _ = Expect.Ok(await store.SaveDecisions(new DecisionBatch(_r103, "v1", [], []), None), test);

        var layer = Expect.Ok(await store.Layer(None), test);
        var only = layer.Decisions.SingleOrDefault();
        Expect.That(only is { Provenance: Provenance.Human } && only.Wording == human.Wording, test, $"{layer.Decisions.Count} decisions: {string.Join(',', layer.Decisions.Select(d => d.Provenance + ":" + d.Wording))}");
        Expect.That(layer.DecisionEdges.Count == 1 && layer.DecisionEdges[0].Provenance == Provenance.Human, test, $"{layer.DecisionEdges.Count} edges");
    }

    /// <summary>An unread pair is overwritten by the next try and by the verdict; a verdict or a refusal is never replaced.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_unread_pair_is_overwritten_by_the_next_try_and_a_settled_pair_never()
    {
        const string test = nameof(An_unread_pair_is_overwritten_by_the_next_try_and_a_settled_pair_never);
        var store = CreateStore();
        var a = NodeKey.From("decision:" + new string('a', 64));
        var b = NodeKey.From("decision:" + new string('b', 64));

        _ = Expect.Ok(await store.SaveConflict(DecisionConflict.Of(a, b, ConflictVerdict.Unread, _at) with { Tries = 1 }, None), test);
        _ = Expect.Ok(await store.SaveConflict(DecisionConflict.Of(a, b, ConflictVerdict.Unread, _at) with { Tries = 2 }, None), test);
        var unread = Expect.Ok(await store.Layer(None), test).Conflicts.Single();
        _ = Expect.Ok(await store.SaveConflict(DecisionConflict.Of(a, b, ConflictVerdict.Refused, _at) with { Tries = 3 }, None), test);
        _ = Expect.Ok(await store.SaveConflict(DecisionConflict.Of(a, b, ConflictVerdict.Agrees, _at), None), test);
        var settled = Expect.Ok(await store.Layer(None), test).Conflicts.Single();

        Expect.That(unread.Verdict == ConflictVerdict.Unread && unread.Tries == 2, test, "unread: " + unread.Verdict + " " + unread.Tries.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Expect.That(settled.Verdict == ConflictVerdict.Refused && settled.Tries == 3, test, "settled: " + settled.Verdict + " " + settled.Tries.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
