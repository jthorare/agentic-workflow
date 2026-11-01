using Aw.Context.Core.Map;
using Xunit;

namespace Aw.Context.Testing.Map;

/// <summary>Issue 1041: the graph store keeps the map rules each version was built under, through current, draft, promote and the publish state. The fake and the SQLite store pass the same tests.</summary>
public abstract class MapRulesetStoreContract
{
    private static CancellationToken None => TestContext.Current.CancellationToken;

    /// <summary>A saved current version reads back with its ruleset, in the version list and in the publish state.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_current_versions_ruleset_reads_back()
    {
        const string test = nameof(A_current_versions_ruleset_reads_back);
        var store = CreateStore();

        _ = Expect.Ok(await store.SaveCurrent(SampleMap.Build() with { Ruleset = "rules-A" }, SampleMap.At, None), test);

        Expect.That(Expect.Ok(await store.Current(None), test)?.Ruleset == "rules-A", test, "the current version lost its ruleset");
        Expect.That(Expect.Ok(await store.Held(None), test).CurrentRuleset == "rules-A", test, "the publish state lost the current ruleset");
    }

    /// <summary>A version saved without a ruleset reads null, so a map built before the rules were recorded counts as older.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_version_saved_without_a_ruleset_reads_null()
    {
        const string test = nameof(A_version_saved_without_a_ruleset_reads_null);
        var store = CreateStore();

        _ = Expect.Ok(await store.SaveCurrent(SampleMap.Build(), SampleMap.At, None), test);

        Expect.That(Expect.Ok(await store.Held(None), test).CurrentRuleset is null, test, "a version saved without a ruleset reported one");
    }

    /// <summary>A promoted draft brings its ruleset to the publish state, and the retained one's stays on its own row.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_promoted_drafts_ruleset_becomes_the_current_one()
    {
        const string test = nameof(A_promoted_drafts_ruleset_becomes_the_current_one);
        var store = CreateStore();
        var first = Expect.Ok(await store.SaveCurrent(SampleMap.Build() with { Ruleset = "rules-A" }, SampleMap.At, None), test);
        var draft = Expect.Ok(await store.SaveDraft(SampleMap.Build("b") with { Ruleset = "rules-B" }, SampleMap.At, None), test);

        Expect.That(Expect.Ok(await store.Held(None), test).CurrentRuleset == "rules-A", test, "a draft changed the current ruleset");
        _ = Expect.Ok(await store.Promote(draft.Number, None), test);

        Expect.That(Expect.Ok(await store.Held(None), test).CurrentRuleset == "rules-B", test, "the promoted draft's ruleset did not become current");
        Expect.That(Expect.Ok(await store.Versions(None), test).Single(v => v.Number == first.Number).Ruleset == "rules-A", test, "the retained version lost its ruleset");
    }

    /// <summary>A draft opened as a copy of the current version carries its ruleset.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_opened_draft_copies_the_ruleset()
    {
        const string test = nameof(An_opened_draft_copies_the_ruleset);
        var store = CreateStore();
        _ = Expect.Ok(await store.SaveCurrent(SampleMap.Build() with { Ruleset = "rules-A" }, SampleMap.At, None), test);

        var draft = Expect.Ok(await store.OpenDraft(SampleMap.At, None), test);

        Expect.That(draft.Ruleset == "rules-A", test, "the copied draft lost the ruleset");
        Expect.That(Expect.Ok(await store.Versions(None), test).Single(v => v.Number == draft.Number).Ruleset == "rules-A", test, "the stored copy lost the ruleset");
    }

    /// <summary>A version keeps the hash of each pin's rule line, through the version list, the publish state and a copied draft; one saved without them reads null (unknown, never empty).</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_versions_pin_hashes_read_back_and_a_version_without_them_reads_null()
    {
        const string test = nameof(A_versions_pin_hashes_read_back_and_a_version_without_them_reads_null);
        var store = CreateStore();
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal) { ["billing"] = "aa", ["auth"] = "bb" };
        var bare = Expect.Ok(await store.SaveCurrent(SampleMap.Build(), SampleMap.At, None), test);

        Expect.That(Expect.Ok(await store.Held(None), test).CurrentPinHashes is null, test, "a version saved without pin hashes reported some");

        var pinned = Expect.Ok(await store.SaveCurrent(SampleMap.Build() with { PinHashes = hashes }, SampleMap.At, None), test);
        var held = Expect.Ok(await store.Held(None), test).CurrentPinHashes;

        Expect.That(held is { Count: 2 } && held["billing"] == "aa" && held["auth"] == "bb", test, "the current version lost its pin hashes");
        Expect.That(Expect.Ok(await store.Versions(None), test).Single(v => v.Number == bare.Number).PinHashes is null, test, "the retained version without them gained some");
        var draft = Expect.Ok(await store.OpenDraft(SampleMap.At, None), test);
        Expect.That(Expect.Ok(await store.Versions(None), test).Single(v => v.Number == draft.Number).PinHashes is { Count: 2 }, test, "the copied draft lost the pin hashes");
        Expect.That(pinned.PinHashes is { Count: 2 }, test, "the saved version did not answer its pin hashes");
    }

    /// <summary>A new store of the kind under test.</summary>
    /// <returns>The store.</returns>
    protected abstract IGraphStore CreateStore();
}
