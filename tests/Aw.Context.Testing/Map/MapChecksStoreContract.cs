using Aw.Context.Core.Map;
using Xunit;

namespace Aw.Context.Testing.Map;

/// <summary>Map structure spec §4.3: the graph store keeps the structure reason with a draft's version row, copies it on promote and on an opened draft, and reads null for a version that has none. The fake and the SQLite store pass the same tests.</summary>
public abstract class MapChecksStoreContract
{
    private static CancellationToken None => TestContext.Current.CancellationToken;

    /// <summary>A draft saved with a reason reads it back.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_drafts_reason_reads_back()
    {
        const string test = nameof(A_drafts_reason_reads_back);
        var store = CreateStore();
        _ = Expect.Ok(await store.SaveCurrent(SampleMap.Build(), SampleMap.At, None), test);

        var draft = Expect.Ok(await store.SaveDraft(SampleMap.Build("b") with { Checks = "structure 41% isolated, limit 30%" }, SampleMap.At, None), test);

        Expect.That(draft.Checks == "structure 41% isolated, limit 30%", test, "the saved draft lost the reason");
        Expect.That(Expect.Ok(await store.Versions(None), test).Single(v => v.Number == draft.Number).Checks == "structure 41% isolated, limit 30%", test, "the stored draft lost the reason");
    }

    /// <summary>A version saved without a reason reads null.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_version_saved_without_a_reason_reads_null()
    {
        const string test = nameof(A_version_saved_without_a_reason_reads_null);
        var store = CreateStore();

        _ = Expect.Ok(await store.SaveCurrent(SampleMap.Build(), SampleMap.At, None), test);

        Expect.That(Expect.Ok(await store.Current(None), test)?.Checks is null, test, "a version saved without a reason reported one");
    }

    /// <summary>A promoted draft is current with its reason, and the retained version keeps its own.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_promoted_drafts_reason_becomes_the_current_one()
    {
        const string test = nameof(A_promoted_drafts_reason_becomes_the_current_one);
        var store = CreateStore();
        var first = Expect.Ok(await store.SaveCurrent(SampleMap.Build(), SampleMap.At, None), test);
        var draft = Expect.Ok(await store.SaveDraft(SampleMap.Build("b") with { Checks = "size 5% over 150 files, limit 2%" }, SampleMap.At, None), test);

        _ = Expect.Ok(await store.Promote(draft.Number, None), test);

        Expect.That(Expect.Ok(await store.Current(None), test)?.Checks == "size 5% over 150 files, limit 2%", test, "the promoted draft's reason did not become current");
        Expect.That(Expect.Ok(await store.Versions(None), test).Single(v => v.Number == first.Number).Checks is null, test, "the retained version gained a reason");
    }

    /// <summary>A replaced draft (what a promote with decisions left rewrites) keeps the reason it was given.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_replaced_draft_keeps_the_reason()
    {
        const string test = nameof(A_replaced_draft_keeps_the_reason);
        var store = CreateStore();
        _ = Expect.Ok(await store.SaveCurrent(SampleMap.Build(), SampleMap.At, None), test);
        var draft = Expect.Ok(await store.SaveDraft(SampleMap.Build("b") with { Checks = "structure 41% isolated, limit 30%" }, SampleMap.At, None), test);

        var replaced = Expect.Ok(await store.ReplaceDraft(draft.Number, SampleMap.Build("c") with { Checks = draft.Checks }, SampleMap.At, None), test);

        Expect.That(replaced.Checks == "structure 41% isolated, limit 30%", test, "the replaced draft lost the reason");
    }

    /// <summary>A new store of the kind under test.</summary>
    /// <returns>The store.</returns>
    protected abstract IGraphStore CreateStore();
}
