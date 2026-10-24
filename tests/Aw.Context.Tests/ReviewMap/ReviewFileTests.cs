using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Review;
using Aw.Context.Testing.Map;
using Aw.Context.Testing.Review;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.ReviewMap;

public sealed class ReviewFileTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private async Task<string> Place(ReviewFile file)
    {
        var path = Path.Combine(FakeReviewFileStore.Folder, "edited.json");
        return (await _world.ReviewFiles.Write(path, "edited.json", file, MapWorld.None)).Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Exporting_writes_the_queue_as_a_review_file_in_the_exports_folder()
    {
        var draft = (await Drafts.Build(_world, clusters: true)).Draft;
        var queue = await Drafts.Queue(_world);

        var exported = (await _world.Map.ExportReview(path: null, Drafts.Scope, MapWorld.None)).Value;

        var file = Assert.Single(_world.ReviewFiles.Files);
        Assert.StartsWith(FakeReviewFileStore.Folder + "/review-v" + draft.Number.ToString(CultureInfo.InvariantCulture), exported.Path, StringComparison.Ordinal);
        Assert.Equal((exported.Path, queue.Entries.Count), (file.Key, exported.Entries));
        Assert.Equal(queue.Draft, file.Value.Draft);
        Assert.All(file.Value.Entries, e => Assert.Null(e.Decision));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_may_export_the_queue()
    {
        _ = await Drafts.Build(_world);

        var exported = await _world.Map.ExportReview(path: null, MapWorld.Session(FixtureMap.Repo), MapWorld.None);

        Assert.True(exported.IsSuccess);   // removed per owner q93 (2026-10-23)
        Assert.NotEmpty(_world.ReviewFiles.Files);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Exporting_inside_a_worktree_is_SND_CONTEXT_005_and_outside_the_folder_033()
    {
        _ = await Drafts.Build(_world);

        var inside = await _world.Map.ExportReview(Path.Combine(FakeReviewFileStore.WorktreeRoot, "review.json"), Drafts.Scope, MapWorld.None);
        var outside = await _world.Map.ExportReview("../elsewhere.json", Drafts.Scope, MapWorld.None);
        using var empty = new MapWorld();
        var none = await empty.Map.ExportReview(path: null, Drafts.Scope, MapWorld.None);

        Assert.Equal((ContextErrors.PathInWorktree.Code, ContextErrors.ExportOutsideHome.Code, ContextErrors.NoDraftMap.Code), (inside.Error.Code, outside.Error.Code, none.Error.Code));
        Assert.Empty(_world.ReviewFiles.Files);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Previewing_a_file_answers_its_hash_and_counts_and_changes_with_an_edit_and_a_session_cant()
    {
        _ = await Drafts.Build(_world);
        var queue = await Drafts.Queue(_world);
        var first = queue.Entries[0];
        var plain = await Place(new ReviewFile(queue.Draft, [new ReviewFileEntry(first.Label, first.Group, first.Confidence, first.Node)]));
        var preview = (await _world.Map.PreviewReviewFile(plain, Drafts.Scope, MapWorld.None)).Value;
        var edited = await Place(new ReviewFile(queue.Draft, [new ReviewFileEntry(first.Label, first.Group, first.Confidence, first.Node, Decision: ReviewVerb.Accept)]));

        var after = (await _world.Map.PreviewReviewFile(edited, Drafts.Scope, MapWorld.None)).Value;
        var session = await _world.Map.PreviewReviewFile(edited, MapWorld.Session(FixtureMap.Repo), MapWorld.None);

        Assert.Equal((1, 0), (preview.Entries, preview.Decided));
        Assert.Equal((1, 1), (after.Entries, after.Decided));
        Assert.NotEqual(preview.Hash, after.Hash, StringComparer.Ordinal);
        Assert.True(session.IsSuccess);
        Assert.Equal(0, await Drafts.Rows(_world));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Applying_a_file_decides_the_entries_that_still_match_and_lists_the_rest_as_skipped()
    {
        _ = await Drafts.Build(_world);
        var queue = await Drafts.Queue(_world);
        var first = queue.Entries[0];
        var second = queue.Entries[1];
        var path = await Place(new ReviewFile(queue.Draft,
        [
            new ReviewFileEntry(first.Label, first.Group, first.Confidence, first.Node, Decision: ReviewVerb.Accept),
            new ReviewFileEntry(second.Label, second.Group, second.Confidence, second.Node, Decision: ReviewVerb.Rename),
            new ReviewFileEntry("Gone", ReviewGroup.Model, 0.5, NodeKey.From("surface:command/not-there"), Decision: ReviewVerb.Reject),
            new ReviewFileEntry("Left alone", ReviewGroup.Deterministic, 1.0, queue.Entries[2].Node),
        ]));

        var result = (await _world.Map.ApplyReviewFile(path, Drafts.Scope, MapWorld.None)).Value;

        Assert.Equal(2, result.Written.Count);
        Assert.Equal(queue.Undecided - 1, result.Undecided);   // the accept decided one; a rename doesn't decide
        var skipped = Assert.Single(result.Skipped);
        Assert.Contains("not-there", skipped, StringComparison.Ordinal);
        Assert.Equal(2, await Drafts.Rows(_world));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_file_with_no_matching_decided_entry_is_SND_CONTEXT_043_and_writes_nothing()
    {
        _ = await Drafts.Build(_world);
        var queue = await Drafts.Queue(_world);
        var path = await Place(new ReviewFile(queue.Draft,
        [
            new ReviewFileEntry("Gone", ReviewGroup.Model, 0.5, NodeKey.From("surface:command/not-there"), Decision: ReviewVerb.Accept),
            new ReviewFileEntry("Undecided", queue.Entries[0].Group, 1.0, queue.Entries[0].Node),
        ]));

        var result = await _world.Map.ApplyReviewFile(path, Drafts.Scope, MapWorld.None);

        Assert.Equal(ContextErrors.ReviewFileNoMatch.Code, result.Error.Code);
        Assert.Equal(0, await Drafts.Rows(_world));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_decision_the_draft_no_longer_allows_is_skipped_not_applied()
    {
        _ = await Drafts.Build(_world);
        var queue = await Drafts.Queue(_world);
        var entry = queue.Entries[0];
        var path = await Place(new ReviewFile(queue.Draft,
        [
            new ReviewFileEntry(entry.Label, entry.Group, entry.Confidence, entry.Node, Decision: ReviewVerb.Move, Other: NodeKey.From("feature:nowhere")),
            new ReviewFileEntry(queue.Entries[1].Label, queue.Entries[1].Group, 1.0, queue.Entries[1].Node, Decision: ReviewVerb.Accept),
        ]));

        var result = (await _world.Map.ApplyReviewFile(path, Drafts.Scope, MapWorld.None)).Value;

        _ = Assert.Single(result.Written);
        _ = Assert.Single(result.Skipped);
        Assert.Equal(1, await Drafts.Rows(_world));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_file_in_a_worktree_or_a_missing_one_is_refused_for_a_session_too()
    {
        _ = await Drafts.Build(_world);
        var inside = await _world.Map.ApplyReviewFile(Path.Combine(FakeReviewFileStore.WorktreeRoot, "review.json"), Drafts.Scope, MapWorld.None);
        var missing = await _world.Map.ApplyReviewFile(Path.Combine(FakeReviewFileStore.Folder, "nothing.json"), Drafts.Scope, MapWorld.None);
        var session = await _world.Map.ApplyReviewFile(Path.Combine(FakeReviewFileStore.Folder, "nothing.json"), MapWorld.Session(FixtureMap.Repo), MapWorld.None);

        Assert.Equal((ContextErrors.PathInWorktree.Code, ContextErrors.MapFileInvalid.Code, ContextErrors.MapFileInvalid.Code), (inside.Error.Code, missing.Error.Code, session.Error.Code));
    }
}
