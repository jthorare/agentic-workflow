using Aw.Context.Core.Surfaces;
using Aw.Context.Testing.Map;
using Xunit;

namespace Aw.Context.Testing.Surfaces;

/// <summary>The surface pick store's contract: the fake and the SQLite store pass the same tests.</summary>
public abstract class SurfacePickStoreContract
{
    private static readonly DateTimeOffset _at = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken None => TestContext.Current.CancellationToken;

    /// <summary>Builds an empty store.</summary>
    /// <returns>The store.</returns>
    protected abstract ISurfacePickStore CreateStore();

    /// <summary>A group key nobody saved is a miss.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_group_key_nobody_saved_is_a_miss()
    {
        const string test = nameof(A_group_key_nobody_saved_is_a_miss);
        var store = CreateStore();

        Expect.That(Expect.Ok(await store.Get(["g1"], None), test).Count == 0, test, "an unsaved key was found");
    }

    /// <summary>Every field reads back, including a merge and a null model.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Saved_decisions_read_back_with_every_field()
    {
        const string test = nameof(Saved_decisions_read_back_with_every_field);
        var store = CreateStore();
        var merge = Pick("g1", SurfaceChoice.Merge, ["action:a.grant", "action:b.grant"], survivorKey: "action:a.grant", mergedName: "Grant access", model: "jev-1");
        var reject = Pick("g2", SurfaceChoice.Reject, ["action:c.x"], at: _at.AddMinutes(1));

        _ = Expect.Ok(await store.Save([merge, reject], None), test);
        var found = Expect.Ok(await store.Get(["g1", "g2"], None), test);

        Expect.That(found.Count == 2, test, "not both decisions came back");
        Expect.That(Same(found["g1"], merge), test, "the merge didn't round-trip");
        Expect.That(Same(found["g2"], reject), test, "the reject with a null model didn't round-trip");
    }

    /// <summary>A second save of the same group key replaces the first.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_second_save_of_the_same_key_replaces_it()
    {
        const string test = nameof(A_second_save_of_the_same_key_replaces_it);
        var store = CreateStore();
        var first = Pick("g1", SurfaceChoice.Reject, ["action:a.x"], model: "jev-1");
        var second = Pick("g1", SurfaceChoice.Separate, ["action:a.x", "action:b.x"], model: "jev-2", at: _at.AddHours(1));

        _ = Expect.Ok(await store.Save([first], None), test);
        _ = Expect.Ok(await store.Save([second], None), test);
        var found = Expect.Ok(await store.Get(["g1"], None), test);

        Expect.That(found.Count == 1 && Same(found["g1"], second), test, "the second save didn't replace the first");
    }

    /// <summary>Get returns only the keys it was asked for.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Get_returns_only_the_asked_keys()
    {
        const string test = nameof(Get_returns_only_the_asked_keys);
        var store = CreateStore();
        _ = Expect.Ok(await store.Save([
            Pick("g1", SurfaceChoice.Separate, ["action:a"]),
            Pick("g2", SurfaceChoice.Reject, ["action:b"])], None), test);

        var found = Expect.Ok(await store.Get(["g2", "g3"], None), test);

        Expect.That(found.Count == 1 && found.ContainsKey("g2"), test, "Get returned keys it wasn't asked for, or missed one");
    }

    private static SurfaceDecision Pick(string key, SurfaceChoice choice, IReadOnlyList<string> candidates, string? survivorKey = null, string? mergedName = null, string? model = null, DateTimeOffset? at = null) =>
        new(key, choice, survivorKey, mergedName, candidates, model, at ?? _at);

    private static bool Same(SurfaceDecision a, SurfaceDecision b) =>
        a.GroupKey == b.GroupKey && a.Choice == b.Choice && a.SurvivorKey == b.SurvivorKey && a.MergedName == b.MergedName
        && a.CandidateKeys.SequenceEqual(b.CandidateKeys, StringComparer.Ordinal) && a.Model == b.Model && a.DecidedAt == b.DecidedAt;
}
