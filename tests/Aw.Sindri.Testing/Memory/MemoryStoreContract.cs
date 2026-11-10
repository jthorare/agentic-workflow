using Aw.Sindri.Core;
using Aw.Sindri.Core.Memory;
using Xunit;

namespace Aw.Sindri.Testing.Memory;

/// <summary>
/// The contract every <see cref="IMemoryStore"/> honours: a memory and a fact read back whole, a name is unique (SND-MEM-002), an unknown memory is SND-MEM-001,
/// refresh moves RefreshedAt, and delete works by key, by tag and for expired facts.
/// </summary>
public abstract class MemoryStoreContract
{
    private static readonly float[] _pair = [0.5f, 0.5f];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A fresh, empty store.</summary>
    /// <returns>The store.</returns>
    protected abstract IMemoryStore Create();

    /// <summary>A created memory reads back whole.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_created_memory_reads_back_whole()
    {
        var store = Create();
        var memory = MemorySamples.Info();

        _ = await store.Create(memory, Token);
        var read = await store.Get(memory.Name, Token);

        Expect.True(read.IsSuccess);
        Expect.Equal(memory, read.Value);
    }

    /// <summary>A second memory with a name is SND-MEM-002.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_name_is_unique()
    {
        var store = Create();
        _ = await store.Create(MemorySamples.Info(), Token);

        var again = await store.Create(MemorySamples.Info(), Token);

        Expect.True(again.IsError);
        Expect.Equal(SindriErrors.MemoryNameTaken.Code, again.Error.Code);
    }

    /// <summary>An unknown memory is SND-MEM-001, for a read and for a fact write.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_unknown_memory_is_not_found()
    {
        var store = Create();

        var read = await store.Get(MemorySamples.Name("nobody"), Token);
        var add = await store.Add(MemorySamples.Fact(memory: MemorySamples.Name("nobody")), Token);

        Expect.Equal(SindriErrors.MemoryNotFound.Code, read.Error.Code);
        Expect.Equal(SindriErrors.MemoryNotFound.Code, add.Error.Code);
    }

    /// <summary>The list is sorted by name.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_list_is_sorted_by_name()
    {
        var store = Create();
        _ = await store.Create(MemorySamples.Info(MemorySamples.Name("zeta")), Token);
        _ = await store.Create(MemorySamples.Info(MemorySamples.Name("alpha")), Token);

        var listed = await store.List(Token);

        Expect.Sequence(["alpha", "zeta"], listed.Value.Select(static m => m.Name.Value));
    }

    /// <summary>A fact reads back whole, embedding and provenance included, and is counted.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_added_fact_reads_back_whole()
    {
        var store = Create();
        var memory = MemorySamples.Info();
        _ = await store.Create(memory, Token);
        var fact = MemorySamples.Fact(tags: ["deploy", "ops"], vector: [0.25f, 0.5f, 0.75f, 1f]);

        _ = await store.Add(fact, Token);
        var facts = await store.Facts(memory.Name, Token);
        var counts = await store.Counts(Token);

        var read = Expect.Only(facts.Value);
        Expect.Equal(fact.Key, read.Key);
        Expect.Equal(fact.Text, read.Text);
        Expect.Equal(fact.Provenance, read.Provenance);
        Expect.Sequence(fact.Tags, read.Tags);
        Expect.Sequence(fact.Embedding.ToArray(), read.Embedding.ToArray());
        Expect.Equal(1, counts.Value[memory.Name.Value]);
    }

    /// <summary>Refresh moves RefreshedAt and keeps one row.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Refresh_moves_the_refreshed_time_and_keeps_one_row()
    {
        var store = Create();
        var memory = MemorySamples.Info();
        _ = await store.Create(memory, Token);
        _ = await store.Add(MemorySamples.Fact(), Token);
        var later = MemorySamples.Fact().CreatedAt.AddHours(2);

        var refreshed = await store.Refresh(memory.Name, "fact-1", later, expiresAt: null, Token);
        var missing = await store.Refresh(memory.Name, "nope", later, expiresAt: null, Token);

        Expect.True(refreshed.Value);
        Expect.True(!missing.Value);
        var read = Expect.Only((await store.Facts(memory.Name, Token)).Value);
        Expect.Equal(later, read.RefreshedAt);
        Expect.Equal(MemorySamples.Fact().CreatedAt, read.CreatedAt);
    }

    /// <summary>Delete by key, by tag and for expired facts removes exactly those.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Delete_works_by_key_by_tag_and_for_expired()
    {
        var store = Create();
        var memory = MemorySamples.Info();
        _ = await store.Create(memory, Token);
        var now = MemorySamples.Fact().CreatedAt.AddDays(1);
        _ = await store.Add(MemorySamples.Fact("a", "alpha"), Token);
        _ = await store.Add(MemorySamples.Fact("b", "beta", tags: ["old"]), Token);
        _ = await store.Add(MemorySamples.Fact("c", "gamma", tags: ["old"]), Token);
        _ = await store.Add(MemorySamples.Fact("d", "delta", expiresAt: now.AddMinutes(-1)), Token);
        _ = await store.Add(MemorySamples.Fact("e", "epsilon", expiresAt: now.AddDays(1)), Token);

        var byKey = await store.Delete(memory.Name, FactSelector.ByKey("a"), Token);
        var byTag = await store.Delete(memory.Name, FactSelector.ByTag("old"), Token);
        var expired = await store.Delete(memory.Name, FactSelector.Expired(now), Token);

        Expect.Equal(1, byKey.Value);
        Expect.Equal(2, byTag.Value);
        Expect.Equal(1, expired.Value);
        Expect.Sequence(["e"], (await store.Facts(memory.Name, Token)).Value.Select(static f => f.Key));
    }

    /// <summary>DeleteExpired sweeps every memory.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task DeleteExpired_sweeps_every_memory()
    {
        var store = Create();
        var now = MemorySamples.Fact().CreatedAt.AddDays(1);
        foreach (var name in new[] { "one", "two" })
        {
            _ = await store.Create(MemorySamples.Info(MemorySamples.Name(name)), Token);
            _ = await store.Add(MemorySamples.Fact($"{name}-old", memory: MemorySamples.Name(name), expiresAt: now.AddMinutes(-5)), Token);
            _ = await store.Add(MemorySamples.Fact($"{name}-new", memory: MemorySamples.Name(name)), Token);
        }

        var swept = await store.DeleteExpired(now, Token);

        Expect.Equal(2, swept.Value);
    }

    /// <summary>A second fact with a key already in the store is refused.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Adding_a_duplicate_key_is_an_error()
    {
        var store = Create();
        _ = await store.Create(MemorySamples.Info(), Token);
        _ = await store.Add(MemorySamples.Fact("same", "one"), Token);

        var again = await store.Add(MemorySamples.Fact("same", "two"), Token);

        Expect.True(again.IsError);
        Expect.Equal(1, (await store.Facts(MemorySamples.Name(), Token)).Value.Count);
    }

    /// <summary>A fact that expires exactly at the sweep time is swept, one a tick later is kept.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_fact_expiring_exactly_now_is_swept_and_one_a_tick_later_is_kept()
    {
        var store = Create();
        _ = await store.Create(MemorySamples.Info(), Token);
        var now = MemorySamples.Fact().CreatedAt.AddDays(1);
        _ = await store.Add(MemorySamples.Fact("at", "at", expiresAt: now), Token);
        _ = await store.Add(MemorySamples.Fact("after", "after", expiresAt: now.AddTicks(1)), Token);

        var swept = await store.DeleteExpired(now, Token);

        Expect.Equal(1, swept.Value);
        Expect.Sequence(["after"], (await store.Facts(MemorySamples.Name(), Token)).Value.Select(static f => f.Key));
    }

    /// <summary>AddOrRefresh refreshes a near duplicate with the same tags and adds anything else.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task AddOrRefresh_refreshes_a_near_duplicate_and_adds_the_rest()
    {
        var store = Create();
        _ = await store.Create(MemorySamples.Info(), Token);
        var later = MemorySamples.Fact().CreatedAt.AddHours(1);

        var first = await store.AddOrRefresh(MemorySamples.Fact("a", "alpha", tags: ["ops"], vector: [1f, 0.2f, 0f, 0f]), Token);
        var near = await store.AddOrRefresh(MemorySamples.Fact("b", "alpha again", tags: ["ops"], vector: [1f, 0.25f, 0f, 0f]) with { RefreshedAt = later }, Token);
        var otherTags = await store.AddOrRefresh(MemorySamples.Fact("c", "alpha", tags: ["dev"], vector: [1f, 0.2f, 0f, 0f]), Token);
        var otherDimension = await store.AddOrRefresh(MemorySamples.Fact("d", "alpha", tags: ["ops"], vector: [1f, 0.2f]), Token);

        Expect.True(!first.Value.Refreshed);
        Expect.True(near.Value.Refreshed);
        Expect.Equal("a", near.Value.Key);
        Expect.True(!otherTags.Value.Refreshed);
        Expect.True(!otherDimension.Value.Refreshed);
        Expect.Equal(3, (await store.Facts(MemorySamples.Name(), Token)).Value.Count);
        Expect.Equal(later, (await store.Facts(MemorySamples.Name(), Token)).Value.First(static f => f.Key == "a").RefreshedAt);
    }

    /// <summary>A refresh by another caller records that caller as the refresher and keeps the first writer.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task AddOrRefresh_records_the_refresher_and_keeps_the_first_writer()
    {
        var store = Create();
        _ = await store.Create(MemorySamples.Info(), Token);
        var later = MemorySamples.Fact().CreatedAt.AddHours(1);

        _ = await store.AddOrRefresh(MemorySamples.Fact("a", "alpha", tags: ["ops"], vector: [1f, 0.2f, 0f, 0f]) with { WrittenBy = "session:launch:A" }, Token);
        Expect.True((await store.Facts(MemorySamples.Name(), Token)).Value.Single().RefreshedBy is null);
        _ = await store.AddOrRefresh(MemorySamples.Fact("b", "alpha again", tags: ["ops"], vector: [1f, 0.25f, 0f, 0f]) with { RefreshedAt = later, WrittenBy = "session:launch:B" }, Token);

        var row = (await store.Facts(MemorySamples.Name(), Token)).Value.Single();
        Expect.Equal("session:launch:A", row.WrittenBy);
        Expect.Equal("session:launch:B", row.RefreshedBy);
    }

    /// <summary>Two near-same writes at once add one fact.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Two_concurrent_near_same_writes_add_one_fact()
    {
        var store = Create();
        _ = await store.Create(MemorySamples.Info(), Token);

        var both = await Task.WhenAll(
            store.AddOrRefresh(MemorySamples.Fact("a", "alpha", tags: ["ops"], vector: [1f, 0.2f, 0f, 0f]), Token).AsTask(),
            store.AddOrRefresh(MemorySamples.Fact("b", "alpha too", tags: ["ops"], vector: [1f, 0.21f, 0f, 0f]), Token).AsTask());

        Expect.True(both.All(static r => r.IsSuccess));
        Expect.Equal(1, (await store.Facts(MemorySamples.Name(), Token)).Value.Count);
    }

    /// <summary>A fact written with no vector is found by Unembedded and takes a vector once, with its model.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Unembedded_finds_facts_without_a_vector_and_SetEmbedding_fills_them_once()
    {
        var store = Create();
        _ = await store.Create(MemorySamples.Info(), Token);
        _ = await store.Add(MemorySamples.Fact("none", "no vector") with { Embedding = ReadOnlyMemory<float>.Empty }, Token);
        _ = await store.Add(MemorySamples.Fact("has", "vector"), Token);

        var waiting = await store.Unembedded(10, Token);
        var set = await store.SetEmbedding(MemorySamples.Name(), "none", _pair, "model@sha", Token);
        var again = await store.SetEmbedding(MemorySamples.Name(), "none", _pair, "model@sha", Token);

        Expect.Sequence(["none"], waiting.Value.Select(static f => f.Key));
        Expect.True(set.Value);
        Expect.True(!again.Value);
        var read = (await store.Facts(MemorySamples.Name(), Token)).Value.First(static f => f.Key == "none");
        Expect.Equal(2, read.Dimension);
        Expect.Equal("model@sha", read.Model);
    }
}
