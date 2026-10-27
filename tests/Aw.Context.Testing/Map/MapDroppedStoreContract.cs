using Aw.Context.Core.Map;
using Aw.Context.Core.Roles;
using Xunit;

namespace Aw.Context.Testing.Map;

/// <summary>Issue 1161: the graph store keeps the files each ignored role dropped with the map version that was built, through current, draft, promote and an opened draft. The fake and the SQLite store pass the same tests.</summary>
public abstract class MapDroppedStoreContract
{
    private static readonly RoleDropped[] _counts = [new("aw-split-panes", "generated", 312), new("aw-split-panes", "docs", 0)];

    private static CancellationToken None => TestContext.Current.CancellationToken;

    /// <summary>A saved current version reads back its counts, in order.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_current_versions_dropped_counts_read_back()
    {
        const string test = nameof(A_current_versions_dropped_counts_read_back);
        var store = CreateStore();

        _ = Expect.Ok(await store.SaveCurrent(SampleMap.Build() with { Dropped = _counts }, SampleMap.At, None), test);

        Expect.That(Expect.Ok(await store.Current(None), test)?.Dropped?.SequenceEqual(_counts) == true, test, "the current version lost its dropped counts");
    }

    /// <summary>A version saved with no counts reads null (built before they were recorded); an empty list reads empty.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_version_saved_without_counts_reads_null_and_one_saved_with_an_empty_list_reads_empty()
    {
        const string test = nameof(A_version_saved_without_counts_reads_null_and_one_saved_with_an_empty_list_reads_empty);
        var store = CreateStore();

        _ = Expect.Ok(await store.SaveCurrent(SampleMap.Build(), SampleMap.At, None), test);
        Expect.That(Expect.Ok(await store.Current(None), test)?.Dropped is null, test, "a version saved without counts reported some");

        _ = Expect.Ok(await store.SaveCurrent(SampleMap.Build("b") with { Dropped = [] }, SampleMap.At, None), test);
        Expect.That(Expect.Ok(await store.Current(None), test)?.Dropped is { Count: 0 }, test, "an empty list did not read back empty");
    }

    /// <summary>A promoted draft brings its counts along, and the retained version keeps its own.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_promoted_drafts_counts_become_the_current_ones()
    {
        const string test = nameof(A_promoted_drafts_counts_become_the_current_ones);
        var store = CreateStore();
        var first = Expect.Ok(await store.SaveCurrent(SampleMap.Build() with { Dropped = _counts }, SampleMap.At, None), test);
        RoleDropped[] later = [new("aw-split-panes", "generated", 7)];
        var draft = Expect.Ok(await store.SaveDraft(SampleMap.Build("b") with { Dropped = later }, SampleMap.At, None), test);

        _ = Expect.Ok(await store.Promote(draft.Number, None), test);

        Expect.That(Expect.Ok(await store.Current(None), test)?.Dropped?.SequenceEqual(later) == true, test, "the promoted draft's counts did not become current");
        Expect.That(Expect.Ok(await store.Versions(None), test).Single(v => v.Number == first.Number).Dropped?.SequenceEqual(_counts) == true, test, "the retained version lost its counts");
    }

    /// <summary>A draft opened as a copy of the current version carries its counts.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_opened_draft_copies_the_counts()
    {
        const string test = nameof(An_opened_draft_copies_the_counts);
        var store = CreateStore();
        _ = Expect.Ok(await store.SaveCurrent(SampleMap.Build() with { Dropped = _counts }, SampleMap.At, None), test);

        var draft = Expect.Ok(await store.OpenDraft(SampleMap.At, None), test);

        Expect.That(Expect.Ok(await store.Versions(None), test).Single(v => v.Number == draft.Number).Dropped?.SequenceEqual(_counts) == true, test, "the stored copy lost the counts");
    }

    /// <summary>A new store of the kind under test.</summary>
    /// <returns>The store.</returns>
    protected abstract IGraphStore CreateStore();
}
