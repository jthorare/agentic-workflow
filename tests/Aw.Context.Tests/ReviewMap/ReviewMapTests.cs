using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Review;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.ReviewMap;

public sealed class ReviewMapTests : IDisposable
{
    private static readonly NodeKey _split = NodeKey.From("surface:command/split-vertical");
    private static readonly NodeKey _close = NodeKey.From("surface:command/close-pane");
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private async Task<Kernel.Result<ReviewResult>> Decide(ReviewDecision decision, ContextScope? scope = null) =>
        await _world.Map.ApplyReview(decision, scope ?? Drafts.Scope, MapWorld.None);

    [Fact, Trait("Lane", "unit")]
    public async Task The_queue_lists_the_deterministic_proposals_first_then_the_model_ones_with_their_files()
    {
        var draft = (await Drafts.Build(_world, clusters: true)).Draft;

        var queue = await Drafts.Queue(_world);

        Assert.Equal(Drafts.Label(draft), queue.Draft);
        Assert.Equal(0, queue.Decided);
        var groups = queue.Entries.Select(e => e.Group).ToList();
        Assert.Equal(groups.Order(), groups);
        Assert.Contains(queue.Entries, e => e.Node.Value == _split.Value && e.Group == ReviewGroup.Deterministic && e.Files.Select(f => f.Value).SequenceEqual(["code:src/commands/ids.ts", "code:src/commands/registry.ts"], StringComparer.Ordinal));
        Assert.Contains(queue.Entries, e => e.Group == ReviewGroup.Model && e.Link is null && e.Node.Kind == NodeKind.Feature);
        Assert.Equal(2, queue.Entries.Count(e => e.Link is not null));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_draft_the_queue_is_SND_CONTEXT_044()
    {
        _ = await _world.ImportFixture();

        var queue = await _world.Map.ReviewQueue(limit: null, Drafts.Scope, MapWorld.None);

        Assert.Equal(ContextErrors.NoDraftMap.Code, queue.Error.Code);
        Assert.Contains("context map propose <file>", queue.Error.Message, StringComparison.Ordinal);
        Assert.Contains("context map build", queue.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_may_read_the_queue_and_decide_in_bulk()
    {
        _ = await Drafts.Build(_world);
        var session = MapWorld.Session(FixtureMap.Repo);

        var queue = await _world.Map.ReviewQueue(limit: null, session, MapWorld.None);
        var bulk = await Decide(new ReviewDecision(ReviewVerb.AcceptDeterministic), session);

        Assert.True(queue.IsSuccess);
        Assert.True(bulk.IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_single_decision_from_a_session_is_left_to_the_class_check_so_the_engine_doesnt_refuse_it()
    {
        _ = await Drafts.Build(_world);

        var decision = await Decide(new ReviewDecision(ReviewVerb.Accept, _split), MapWorld.Session(FixtureMap.Repo));

        Assert.True(decision.IsSuccess);
        Assert.Equal(1, await Drafts.Rows(_world));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Accepting_a_node_writes_exactly_one_row_and_takes_it_out_of_the_queue()
    {
        _ = await Drafts.Build(_world);
        var before = await Drafts.Queue(_world);

        var result = (await Decide(new ReviewDecision(ReviewVerb.Accept, _split))).Value;

        var row = Assert.Single((await _world.Store.Overrides(MapWorld.None)).Value);
        Assert.Equal((OverrideAction.Accept, _split.Value, "avery"), (row.Action, row.Target.Value, row.Who));
        Assert.Equal([row.ToId().Value], result.Written.Select(w => w.Value), StringComparer.Ordinal);
        Assert.Equal(before.Undecided - 1, result.Undecided);
        Assert.DoesNotContain((await Drafts.Queue(_world)).Entries, e => e.Node.Value == _split.Value);
        Assert.Equal(1, (await Drafts.Queue(_world)).Decided);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Rejecting_a_node_records_what_was_rejected_so_a_rebuild_does_not_bring_it_back()
    {
        _ = await Drafts.Build(_world);

        _ = (await Decide(new ReviewDecision(ReviewVerb.Reject, _close))).Value;
        _ = await Drafts.Build(_world);

        var row = Assert.Single((await _world.Store.Overrides(MapWorld.None)).Value);
        Assert.NotNull(row.Rejection);
        Assert.DoesNotContain((await Drafts.Queue(_world)).Entries, e => e.Node.Value == _close.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Accepting_every_deterministic_proposal_writes_one_row_per_entry_and_leaves_the_model_ones()
    {
        _ = await Drafts.Build(_world, clusters: true);
        var before = await Drafts.Queue(_world);
        var deterministic = before.Entries.Count(e => e.Group == ReviewGroup.Deterministic);

        var result = (await Decide(new ReviewDecision(ReviewVerb.AcceptDeterministic))).Value;

        Assert.True(deterministic >= 4);
        Assert.Equal(deterministic, result.Written.Count);
        Assert.Equal(deterministic, await Drafts.Rows(_world));
        Assert.Equal(before.Undecided - deterministic, result.Undecided);
        Assert.All((await Drafts.Queue(_world)).Entries, e => Assert.Equal(ReviewGroup.Model, e.Group));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_suggested_link_is_accepted_by_its_id_in_one_row()
    {
        _ = await Drafts.Build(_world, clusters: true);
        var link = (await Drafts.Queue(_world)).Entries.First(e => e.Link is not null);

        var result = (await Decide(new ReviewDecision(ReviewVerb.Accept, Link: link.Link))).Value;

        var row = Assert.Single((await _world.Store.Overrides(MapWorld.None)).Value);
        Assert.Equal(link.Link, row.Link);
        _ = Assert.Single(result.Written);
        Assert.DoesNotContain((await Drafts.Queue(_world)).Entries, e => e.Link == link.Link);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Rename_and_move_are_one_row_each()
    {
        _ = await Drafts.Build(_world, clusters: true);
        var feature = (await Drafts.Queue(_world)).Entries.First(e => e.Group == ReviewGroup.Model && e.Link is null).Node;

        _ = (await Decide(new ReviewDecision(ReviewVerb.Rename, _split, Label: "Split the pane vertically"))).Value;
        _ = (await Decide(new ReviewDecision(ReviewVerb.Move, _close, Other: feature))).Value;

        var rows = (await _world.Store.Overrides(MapWorld.None)).Value;
        Assert.Equal(2, rows.Count);
        Assert.Equal("Split the pane vertically", rows.Single(r => r.Action == OverrideAction.Rename).Label);
        Assert.Equal(feature.Value, rows.Single(r => r.Action == OverrideAction.Move).Other!.Value.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_decided_or_unknown_target_is_SND_CONTEXT_047_and_writes_nothing()
    {
        _ = await Drafts.Build(_world);
        _ = await Decide(new ReviewDecision(ReviewVerb.Accept, _split));

        var again = await Decide(new ReviewDecision(ReviewVerb.Accept, _split));
        var unknown = await Decide(new ReviewDecision(ReviewVerb.Reject, NodeKey.From("surface:command/never-found")));
        var linkGone = await Decide(new ReviewDecision(ReviewVerb.Accept, Link: LinkId.From("edge:0123456789abcdef")));

        Assert.All([again, unknown, linkGone], r => Assert.Equal(ContextErrors.ReviewTargetStale.Code, r.Error.Code));
        Assert.Equal(1, await Drafts.Rows(_world));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Undo_reverses_one_decision_with_a_row_of_its_own_and_the_item_is_back_in_the_queue()
    {
        _ = await Drafts.Build(_world);
        var accepted = (await Decide(new ReviewDecision(ReviewVerb.Accept, _split))).Value.Written[0];

        var undone = (await Decide(new ReviewDecision(ReviewVerb.Undo, Undoes: accepted))).Value;
        var twice = await Decide(new ReviewDecision(ReviewVerb.Undo, Undoes: accepted));

        Assert.Equal(2, await Drafts.Rows(_world));
        _ = Assert.Single(undone.Written);
        Assert.Contains((await Drafts.Queue(_world)).Entries, e => e.Node.Value == _split.Value);
        Assert.Equal(ContextErrors.ReviewTargetStale.Code, twice.Error.Code);
        Assert.Equal(2, await Drafts.Rows(_world));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_orphaned_override_is_dropped_with_an_undo_row()
    {
        _ = await Drafts.Build(_world);
        var orphan = new MapOverride(OverrideAction.Accept, NodeKey.From("surface:command/long-gone"), "avery", DateTimeOffset.Parse("2026-10-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        _ = await _world.Store.AppendOverrides([orphan], MapWorld.None);
        var entry = Assert.Single((await Drafts.Queue(_world)).Entries, e => e.Group == ReviewGroup.Orphaned);

        var dropped = (await Decide(new ReviewDecision(ReviewVerb.Drop, Undoes: entry.Override))).Value;

        _ = Assert.Single(dropped.Written);
        Assert.DoesNotContain((await Drafts.Queue(_world)).Entries, e => e.Group == ReviewGroup.Orphaned);
        Assert.Equal(2, await Drafts.Rows(_world));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_history_lists_decisions_newest_first_and_a_session_cant_read_it()
    {
        _ = await Drafts.Build(_world);
        var accepted = (await Decide(new ReviewDecision(ReviewVerb.Accept, _split))).Value.Written.Single();
        var rejected = (await Decide(new ReviewDecision(ReviewVerb.Reject, _close))).Value.Written.Single();

        var history = await _world.Map.ReviewHistory(limit: null, Drafts.Scope, MapWorld.None);
        var session = await _world.Map.ReviewHistory(limit: null, MapWorld.Session(FixtureMap.Repo), MapWorld.None);

        Assert.Equal(new[] { rejected.Value, accepted.Value }.Order(StringComparer.Ordinal), history.Value.Entries.Select(e => e.Id.Value).Order(StringComparer.Ordinal));
        Assert.True(session.IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_orphaned_override_is_repointed_with_an_undo_row_and_a_copy_on_the_new_node()
    {
        _ = await Drafts.Build(_world);
        var orphan = new MapOverride(OverrideAction.Rename, NodeKey.From("surface:command/long-gone"), "avery", DateTimeOffset.Parse("2026-10-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture), "Split");
        _ = await _world.Store.AppendOverrides([orphan], MapWorld.None);
        var entry = Assert.Single((await Drafts.Queue(_world)).Entries, e => e.Group == ReviewGroup.Orphaned);

        var moved = (await Decide(new ReviewDecision(ReviewVerb.Repoint, _split, Undoes: entry.Override))).Value;

        Assert.Equal(2, moved.Written.Count);
        Assert.DoesNotContain((await Drafts.Queue(_world)).Entries, e => e.Group == ReviewGroup.Orphaned);
        Assert.Contains((await _world.Store.Overrides(MapWorld.None)).Value, o => o.Action == OverrideAction.Rename && o.Target.Value == _split.Value && o.Label == "Split");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_link_in_the_current_map_is_decided_when_there_is_no_draft()
    {
        _ = await _world.ImportYaml(MapWorld.TabsMap);
        var link = LinkIds.For(new GraphEdge(NodeKey.From("code:src/tabs/TabGroups.ts"), NodeKey.From("feature:tabs.tab-groups"), EdgeType.Implements));

        var result = (await Decide(new ReviewDecision(ReviewVerb.Accept, Link: link))).Value;

        var row = Assert.Single((await _world.Store.Overrides(MapWorld.None)).Value, o => o.Link is not null);
        Assert.Equal((link, 1), (row.Link!.Value, result.Written.Count));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_file_map_link_decision_is_undone_against_the_current_map_when_there_is_no_draft()
    {
        _ = await _world.ImportYaml(MapWorld.TabsMap);
        var link = LinkIds.For(new GraphEdge(NodeKey.From("code:src/tabs/TabGroups.ts"), NodeKey.From("feature:tabs.tab-groups"), EdgeType.Implements));
        var accepted = (await Decide(new ReviewDecision(ReviewVerb.Accept, Link: link))).Value.Written.Single();
        _world.Clock.Advance(TimeSpan.FromMinutes(1));

        var undone = await Decide(new ReviewDecision(ReviewVerb.Undo, Undoes: accepted));

        Assert.True(undone.IsSuccess, undone.IsError ? undone.Error.Message : null);
        Assert.Contains((await _world.Store.Overrides(MapWorld.None)).Value, o => o.Action == OverrideAction.Undo && o.Undoes == accepted);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_orphaned_override_of_the_current_map_is_dropped_with_no_draft()
    {
        _ = await _world.ImportYaml(MapWorld.TabsMap);
        var orphan = new MapOverride(OverrideAction.Accept, NodeKey.From("surface:command/long-gone"), "avery", DateTimeOffset.Parse("2026-10-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        _ = await _world.Store.AppendOverrides([orphan], MapWorld.None);

        var dropped = await Decide(new ReviewDecision(ReviewVerb.Drop, Undoes: orphan.ToId()));

        Assert.True(dropped.IsSuccess, dropped.IsError ? dropped.Error.Message : null);
        Assert.Contains((await _world.Store.Overrides(MapWorld.None)).Value, o => o.Action == OverrideAction.Undo && o.Undoes == orphan.ToId());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_orphaned_override_of_the_current_map_is_repointed_with_no_draft()
    {
        _ = await _world.ImportYaml(MapWorld.TabsMap);
        var orphan = new MapOverride(OverrideAction.Rename, NodeKey.From("surface:command/long-gone"), "avery", DateTimeOffset.Parse("2026-10-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture), "Tab groups");
        _ = await _world.Store.AppendOverrides([orphan], MapWorld.None);

        var moved = await Decide(new ReviewDecision(ReviewVerb.Repoint, NodeKey.From("feature:tabs.tab-groups"), Undoes: orphan.ToId()));

        Assert.True(moved.IsSuccess, moved.IsError ? moved.Error.Message : null);
        Assert.Equal(2, moved.Value.Written.Count);
        var rows = (await _world.Store.Overrides(MapWorld.None)).Value;
        Assert.Contains(rows, o => o.Action == OverrideAction.Undo && o.Undoes == orphan.ToId());
        Assert.Contains(rows, o => o.Action == OverrideAction.Rename && o.Target.Value == "feature:tabs.tab-groups" && o.Label == "Tab groups");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_list_undo_works_with_no_draft_too()
    {
        _ = await _world.ImportYaml(MapWorld.TabsMap);
        var link = LinkIds.For(new GraphEdge(NodeKey.From("code:src/tabs/TabGroups.ts"), NodeKey.From("feature:tabs.tab-groups"), EdgeType.Implements));
        var accepted = (await Decide(new ReviewDecision(ReviewVerb.Accept, Link: link))).Value.Written.Single();
        _world.Clock.Advance(TimeSpan.FromMinutes(1));

        var undone = await Decide(new ReviewDecision(ReviewVerb.Undo, UndoesAll: [accepted]));

        Assert.True(undone.IsSuccess, undone.IsError ? undone.Error.Message : null);
        _ = Assert.Single(undone.Value.Written);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Undo_of_a_list_undoes_every_decision_with_one_row_each_in_one_call()
    {
        _ = await Drafts.Build(_world);
        var first = (await Decide(new ReviewDecision(ReviewVerb.Accept, _split))).Value.Written.Single();
        var second = (await Decide(new ReviewDecision(ReviewVerb.Reject, _close))).Value.Written.Single();
        _world.Clock.Advance(TimeSpan.FromMinutes(1));   // why: the clock stands still, and an undo is ordered after what it undoes by its time

        var undone = (await Decide(new ReviewDecision(ReviewVerb.Undo, UndoesAll: [first, second]))).Value;

        Assert.Equal(2, undone.Written.Count);
        Assert.Equal(4, await Drafts.Rows(_world));
        var queue = await Drafts.Queue(_world);
        Assert.Contains(queue.Entries, e => e.Node.Value == _split.Value);
        Assert.Contains(queue.Entries, e => e.Node.Value == _close.Value);
        Assert.Equal(queue.Undecided, undone.Undecided);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Undo_list_with_one_undone_id_is_047_and_writes_nothing()
    {
        _ = await Drafts.Build(_world);
        var first = (await Decide(new ReviewDecision(ReviewVerb.Accept, _split))).Value.Written.Single();
        var second = (await Decide(new ReviewDecision(ReviewVerb.Reject, _close))).Value.Written.Single();
        _world.Clock.Advance(TimeSpan.FromMinutes(1));
        _ = await Decide(new ReviewDecision(ReviewVerb.Undo, Undoes: second));
        _world.Clock.Advance(TimeSpan.FromMinutes(1));
        var before = await Drafts.Rows(_world);

        var refused = await Decide(new ReviewDecision(ReviewVerb.Undo, UndoesAll: [first, second]));

        Assert.Equal(ContextErrors.ReviewTargetStale.Code, refused.Error.Code);
        Assert.Contains(second.Value, refused.Error.Message, StringComparison.Ordinal);
        Assert.Equal(before, await Drafts.Rows(_world));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_queue_names_its_draft_version_and_commit_and_counts_by_group_and_kind()
    {
        var draft = (await Drafts.Build(_world)).Draft;

        var queue = await Drafts.Queue(_world);

        Assert.Equal((draft.Number, MapWorld.Head[..7]), (queue.Version.Number, queue.Version.Commit));
        Assert.Equal(queue.Entries.Count(e => e.Group == ReviewGroup.Deterministic), queue.Counts.Deterministic);
        Assert.Equal(queue.Counts.Deterministic, queue.ByKind.Values.Sum());
        Assert.Equal(queue.Decided + queue.Counts.Deterministic + queue.Counts.Model, queue.Proposals);
        Assert.Equal(ReviewTruncated.None, queue.Truncated);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Queue_caps_each_group_and_keeps_full_counts_over_the_facade()
    {
        _ = await Drafts.Build(_world);
        var full = await Drafts.Queue(_world);

        var capped = (await _world.Map.ReviewQueue(limit: 1, Drafts.Scope, MapWorld.None)).Value;

        Assert.True(full.Counts.Deterministic > 1);
        Assert.Equal(1, capped.Entries.Count(e => e.Group == ReviewGroup.Deterministic));
        Assert.Equal(full.Counts, capped.Counts);
        Assert.True(capped.Truncated.Deterministic);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(1001)]
    public async Task A_limit_outside_1_to_1000_is_006_for_the_queue_and_the_history(int limit)
    {
        _ = await Drafts.Build(_world);

        var queue = await _world.Map.ReviewQueue(limit, Drafts.Scope, MapWorld.None);
        var history = await _world.Map.ReviewHistory(limit, Drafts.Scope, MapWorld.None);

        Assert.Equal((ContextErrors.QueryInvalid.Code, ContextErrors.QueryInvalid.Code), (queue.Error.Code, history.Error.Code));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_history_limit_keeps_the_newest_decisions_and_says_so()
    {
        _ = await Drafts.Build(_world);
        _ = await Decide(new ReviewDecision(ReviewVerb.Accept, _split));
        _world.Clock.Advance(TimeSpan.FromMinutes(1));
        var newest = (await Decide(new ReviewDecision(ReviewVerb.Reject, _close))).Value.Written.Single();

        var history = (await _world.Map.ReviewHistory(limit: 1, Drafts.Scope, MapWorld.None)).Value;

        Assert.Equal(newest, Assert.Single(history.Entries).Id);
        Assert.True(history.Truncated);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_refused_link_decision_with_no_draft_points_at_the_current_map_and_not_at_the_queue()
    {
        _ = await _world.ImportYaml(MapWorld.TabsMap);

        var refused = await Decide(new ReviewDecision(ReviewVerb.Accept, Link: LinkId.From("edge:0123456789abcdef")));

        Assert.Equal(ContextErrors.ReviewTargetStale.Code, refused.Error.Code);
        Assert.Contains("the current map", refused.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("map review'", refused.Error.Message, StringComparison.Ordinal);
    }
}
