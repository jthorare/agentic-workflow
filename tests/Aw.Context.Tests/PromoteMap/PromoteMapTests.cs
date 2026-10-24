using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.Review;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;
using Aw.Context.Tests.ReviewMap;

namespace Aw.Context.Tests.PromoteMap;

public sealed class PromoteMapTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private async Task Decide(ReviewDecision decision) => Assert.True((await _world.Map.ApplyReview(decision, Drafts.Scope, MapWorld.None)).IsSuccess);

    [Fact, Trait("Lane", "unit")]
    public async Task Promoting_with_no_draft_is_SND_CONTEXT_046_and_names_the_fix()
    {
        var promoted = await _world.Map.Promote(Drafts.Scope, MapWorld.None);

        Assert.Equal(ContextErrors.PromoteRefused.Code, promoted.Error.Code);
        Assert.Contains("fix:", promoted.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_draft_with_undecided_items_is_SND_CONTEXT_046_and_nothing_changes()
    {
        var draft = (await Drafts.Build(_world)).Draft;
        var queue = await Drafts.Queue(_world);

        var promoted = await _world.Map.Promote(Drafts.Scope, MapWorld.None);

        Assert.Equal(ContextErrors.PromoteRefused.Code, promoted.Error.Code);
        Assert.Contains(queue.Undecided.ToString(CultureInfo.InvariantCulture) + " undecided", promoted.Error.Message, StringComparison.Ordinal);
        Assert.Equal([(draft.Number, MapVersionStatus.Draft)], (await _world.Store.Versions(MapWorld.None)).Value.Select(v => (v.Number, v.Status)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rebuild_that_finds_new_model_items_needs_them_decided_before_it_can_be_promoted()
    {
        _ = await Drafts.Build(_world);
        await Decide(new ReviewDecision(ReviewVerb.AcceptDeterministic));
        _ = await Drafts.Build(_world, clusters: true);   // the rebuild keeps the decisions; the model items are new
        await Decide(new ReviewDecision(ReviewVerb.AcceptDeterministic));

        var promoted = await _world.Map.Promote(Drafts.Scope, MapWorld.None);

        Assert.Equal(ContextErrors.PromoteRefused.Code, promoted.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Once_every_item_is_decided_promote_makes_the_draft_current_and_retains_the_old_one()
    {
        _ = await Drafts.Build(_world);
        await Decide(new ReviewDecision(ReviewVerb.AcceptDeterministic));
        var first = (await _world.Map.Promote(Drafts.Scope, MapWorld.None)).Value;
        var second = (await Drafts.Build(_world)).Draft;
        await Decide(new ReviewDecision(ReviewVerb.AcceptDeterministic));

        var promoted = (await _world.Map.Promote(Drafts.Scope, MapWorld.None)).Value;

        Assert.Equal(MapVersionStatus.Current, first.Version.Status);
        Assert.Null(first.Retained);
        Assert.Equal((second.Number, MapVersionStatus.Current), (promoted.Version.Number, promoted.Version.Status));
        Assert.Equal((first.Version.Number, MapVersionStatus.Retained), (promoted.Retained!.Number, promoted.Retained.Status));
        var versions = (await _world.Store.Versions(MapWorld.None)).Value;
        _ = Assert.Single(versions, v => v.Status == MapVersionStatus.Current);
        Assert.DoesNotContain(versions, v => v.Status == MapVersionStatus.Draft);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_promoted_map_is_what_status_and_queries_read()
    {
        _ = await Drafts.Build(_world);
        await Decide(new ReviewDecision(ReviewVerb.AcceptDeterministic));

        var promoted = (await _world.Map.Promote(Drafts.Scope, MapWorld.None)).Value;

        var status = (await _world.Map.MapStatus(anchor: null, Drafts.Scope, MapWorld.None)).Value;
        Assert.Equal(promoted.Version.Number, status.Version.Number);
        Assert.True(status.Counts.Nodes > 0);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_may_promote()
    {
        _ = await Drafts.Build(_world);
        await Decide(new ReviewDecision(ReviewVerb.AcceptDeterministic));

        var promoted = await _world.Map.Promote(MapWorld.Session(FixtureMap.Repo), MapWorld.None);

        Assert.True(promoted.IsSuccess);
        Assert.DoesNotContain((await _world.Store.Versions(MapWorld.None)).Value, v => v.Status == MapVersionStatus.Draft);
    }

    private static ContextScope DenyingPrivate() =>
        new(Settings: null, [new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes") { DenyPaths = [.. PathDenylist.Defaults, "private/**"] }],
            new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli));

    // A draft the deny glob has to cut: the promote writes a cut copy of it, which must not replace a draft another build made since.
    private async Task<MapVersion> CutDraft()
    {
        _ = await _world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            "  - {key: \"feature:kept\", label: \"Kept\"}",
            "  - {key: \"code:private/a.ts\", label: \"a.ts\"}",
            "edges:",
            "  - {from: \"code:private/a.ts\", to: \"feature:kept\", type: implements}"));
        return (await _world.Store.OpenDraft(_world.Clock.GetUtcNow(), MapWorld.None)).Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_cut_promote_refuses_when_a_rebuild_replaced_the_draft_after_it_was_read_and_keeps_the_rebuild()
    {
        var read = await CutDraft();
        var rebuilt = 0;
        _world.Store.AfterSnapshot = draft =>
        {
            if (draft && rebuilt == 0)
            {
                rebuilt = _world.Store.SaveDraft(SampleMap.Build(" (rebuilt)"), _world.Clock.GetUtcNow(), MapWorld.None).AsTask().GetAwaiter().GetResult().Value.Number;
            }
        };

        var promoted = await _world.Map.Promote(DenyingPrivate(), MapWorld.None);

        Assert.Equal(ContextErrors.PromoteRefused.Code, promoted.Error.Code);
        Assert.Contains("changed", promoted.Error.Message, StringComparison.Ordinal);
        Assert.Contains("fix:", promoted.Error.Message, StringComparison.Ordinal);
        var versions = (await _world.Store.Versions(MapWorld.None)).Value;
        Assert.Equal(rebuilt, Assert.Single(versions, v => v.Status == MapVersionStatus.Draft).Number);
        Assert.NotEqual(read.Number, rebuilt);
        _ = Assert.Single(versions, v => v.Status == MapVersionStatus.Current);
        Assert.Equal(Canonical.Of(SampleMap.Build(" (rebuilt)").Graph), Canonical.Of((await _world.Store.Read(rebuilt, MapWorld.None)).Value));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_cut_promote_with_no_rebuild_in_between_promotes_the_cut_copy()
    {
        _ = await CutDraft();

        var promoted = await _world.Map.Promote(DenyingPrivate(), MapWorld.None);

        Assert.True(promoted.IsSuccess, promoted.IsError ? promoted.Error.Message : null);
        var current = (await _world.Store.Snapshot(draft: false, MapWorld.None)).Value!;
        Assert.DoesNotContain(current.Graph.Nodes, n => n.Key.Value.Contains("private/", StringComparison.Ordinal));
    }
}
