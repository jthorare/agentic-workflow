using Aw.Judge.Core.Batching;
using Aw.Judge.Core.Ports;
using Xunit;

namespace Aw.Judge.Testing;

/// <summary>
/// The ITraitVerdictCache contract: the same tests run against the in-memory fake and the SQLite adapter
/// (.agents/rules/csharp.md, Seams). A derived class supplies a fresh, empty cache on the given clock and bounds, and
/// one that can't be used.
/// </summary>
public abstract class TraitVerdictCacheContract
{
    private static readonly CancellationToken _none = CancellationToken.None;
    private static readonly TimeSpan _week = TimeSpan.FromDays(7);
    private static readonly DateTimeOffset _start = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>A fresh, empty cache.</summary>
    /// <param name="clock">When a verdict is stored and read.</param>
    /// <param name="keep">How long a verdict is used.</param>
    /// <param name="maxEntries">The most verdicts kept.</param>
    /// <returns>The cache.</returns>
    protected abstract ITraitVerdictCache Create(TimeProvider clock, TimeSpan keep, int maxEntries);

    /// <summary>A cache that can't be opened (a location that can't be written).</summary>
    /// <returns>The cache.</returns>
    protected abstract ITraitVerdictCache CreateUnavailable();

    /// <summary>An unknown key is a miss.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_unknown_key_is_a_miss()
    {
        var cache = Create(new VerdictCacheClock(_start), _week, 10);

        var found = await cache.Get(["k1"], _none);

        Expect.That(found.Count == 0, "an unknown key is a miss");
    }

    /// <summary>A stored verdict reads back with its choice, confidence and probabilities.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_stored_verdict_reads_back_whole()
    {
        var cache = Create(new VerdictCacheClock(_start), _week, 10);
        var verdict = Verdict("violation", 0.875);

        await cache.Put(new Dictionary<string, ChoiceBatchAnswer>(StringComparer.Ordinal) { ["k1"] = verdict }, _none);
        var found = await cache.Get(["k1", "k2"], _none);

        Expect.That(found.Count == 1 && found.ContainsKey("k1"), "only the stored key is found");
        var only = found["k1"];
        Expect.That(only.Choice == verdict.Choice && only.Confidence.Equals(verdict.Confidence), "the choice and confidence read back");
        Expect.That(only.Probabilities.Count == 2 && only.Probabilities["violation"].Equals(0.875) && only.Probabilities["other"].Equals(0.125), "the probabilities read back");
    }

    /// <summary>Storing a key again replaces its verdict.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Storing_a_key_again_replaces_it()
    {
        var cache = Create(new VerdictCacheClock(_start), _week, 10);

        await cache.Put(new Dictionary<string, ChoiceBatchAnswer>(StringComparer.Ordinal) { ["k1"] = Verdict("violation", 0.9) }, _none);
        await cache.Put(new Dictionary<string, ChoiceBatchAnswer>(StringComparer.Ordinal) { ["k1"] = Verdict("not-applicable", 0.7) }, _none);

        Expect.That((await cache.Get(["k1"], _none))["k1"].Choice == "not-applicable", "the second verdict replaced the first");
    }

    /// <summary>A verdict older than the keep window is a miss.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_verdict_past_the_keep_window_is_a_miss()
    {
        var clock = new VerdictCacheClock(_start);
        var cache = Create(clock, _week, 10);
        await cache.Put(new Dictionary<string, ChoiceBatchAnswer>(StringComparer.Ordinal) { ["k1"] = Verdict("violation", 0.9) }, _none);

        clock.Advance(TimeSpan.FromDays(6));
        Expect.That((await cache.Get(["k1"], _none)).Count == 1, "six days on, the verdict is used");

        clock.Advance(TimeSpan.FromDays(2));
        Expect.That((await cache.Get(["k1"], _none)).Count == 0, "eight days on, it is a miss");
    }

    /// <summary>Past the size bound, the oldest verdicts go and the newest stay.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Past_the_size_bound_the_newest_verdicts_stay()
    {
        var clock = new VerdictCacheClock(_start);
        var cache = Create(clock, _week, 2);
        foreach (var key in new[] { "k1", "k2", "k3" })
        {
            await cache.Put(new Dictionary<string, ChoiceBatchAnswer>(StringComparer.Ordinal) { [key] = Verdict("violation", 0.9) }, _none);
            clock.Advance(TimeSpan.FromSeconds(1));
        }

        var found = await cache.Get(["k1", "k2", "k3"], _none);

        Expect.That(string.Join(',', found.Keys.Order(StringComparer.Ordinal)) == "k2,k3", "the oldest verdict went, the two newest stay");
    }

    /// <summary>A cache that can't be opened misses and drops writes, and never throws.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_unusable_cache_misses_and_never_throws()
    {
        var cache = CreateUnavailable();

        await cache.Put(new Dictionary<string, ChoiceBatchAnswer>(StringComparer.Ordinal) { ["k1"] = Verdict("violation", 0.9) }, _none);

        Expect.That((await cache.Get(["k1"], _none)).Count == 0, "nothing is found, and nothing threw");
    }

    private static ChoiceBatchAnswer Verdict(string choice, double p) =>
        new(choice, p, new Dictionary<string, double>(StringComparer.Ordinal) { [choice] = p, ["other"] = 1 - p });
}
