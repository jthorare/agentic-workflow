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
}
