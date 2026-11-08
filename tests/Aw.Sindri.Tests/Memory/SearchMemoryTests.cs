using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Memory;
using Aw.Sindri.Features.SearchMemory;
using Aw.Sindri.Testing.Memory;

namespace Aw.Sindri.Tests.Memory;

[Lane(TestLane.Unit)]
public sealed class SearchMemoryTests
{
    private static readonly (string Key, float[] Vector)[] _vectors = [("close", [1f, 0.1f, 0f, 0f]), ("middle", [0.6f, 0.8f, 0f, 0f]), ("far", [0.2f, 0.98f, 0f, 0f])];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<MemoryHarness> Seeded()
    {
        var h = new MemoryHarness();
        await h.Memory();
        // cosine to the query [1,0,0,0]: close 0.995, middle 0.6, far 0.2
        foreach (var (key, vector) in _vectors)
        {
            _ = await h.Store.Add(MemorySamples.Fact(key, $"text of {key}", vector: vector), Token);
        }

        _ = h.Embedder.With("what do we know", 1f, 0f, 0f, 0f);
        return h;
    }

    [Fact]
    public async Task Recall_ranks_by_cosine_and_respects_loose_balanced_and_strict()
    {
        var h = await Seeded();

        var loose = await h.Search().Handle(new SearchMemoryRequest("team-notes", "what do we know", "loose", Limit: null), Token);
        var balanced = await h.Search().Handle(new SearchMemoryRequest("team-notes", "what do we know", "balanced", Limit: null), Token);
        var strict = await h.Search().Handle(new SearchMemoryRequest("team-notes", "what do we know", "strict", Limit: null), Token);

        Assert.Equal("semantic", loose.Value.Mode);
        Assert.Equal(["close", "middle", "far"], loose.Value.Facts.Select(static f => f.Key), StringComparer.Ordinal);
        Assert.Equal(["close", "middle"], balanced.Value.Facts.Select(static f => f.Key), StringComparer.Ordinal);
        Assert.Equal(["close"], strict.Value.Facts.Select(static f => f.Key), StringComparer.Ordinal);
        Assert.True(loose.Value.Facts[0].Score > loose.Value.Facts[1].Score);
    }

    [Fact]
    public async Task Without_the_model_recall_falls_back_to_keywords_and_says_so()
    {
        var h = await Seeded();
        h.Embedder.Off = true;

        var result = await h.Search().Handle(new SearchMemoryRequest("team-notes", "text of middle", "balanced", Limit: null), Token);

        Assert.Equal("keyword", result.Value.Mode);
        Assert.Contains("middle", result.Value.Facts.Select(static f => f.Key), StringComparer.Ordinal);
        Assert.Contains(SindriErrors.MemoryEmbedUnavailable.Title, result.Value.Note ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Recalled_facts_carry_their_provenance()
    {
        var h = await Seeded();

        var result = await h.Search().Handle(new SearchMemoryRequest("team-notes", "what do we know", "loose", Limit: 1), Token);

        var fact = Assert.Single(result.Value.Facts);
        Assert.Equal(new MemoryProvenance("wf-1", "run-1", "item-1", "session-1"), fact.Provenance);
        Assert.Equal(1, result.Value.Count);
    }

    [Fact]
    public async Task An_expired_fact_is_never_recalled()
    {
        var h = await Seeded();
        _ = await h.Store.Add(MemorySamples.Fact("stale", "stale text", expiresAt: h.Clock.GetUtcNow().AddMinutes(-1), vector: [1f, 0f, 0f, 0f]), Token);

        var result = await h.Search().Handle(new SearchMemoryRequest("team-notes", "what do we know", "loose", Limit: null), Token);

        Assert.DoesNotContain("stale", result.Value.Facts.Select(static f => f.Key), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_bad_level_is_SND_MEM_005_and_an_unknown_memory_SND_MEM_001()
    {
        var h = await Seeded();

        var level = await h.Search().Handle(new SearchMemoryRequest("team-notes", "q", "tight", Limit: null), Token);
        var unknown = await h.Search().Handle(new SearchMemoryRequest("nobody", "q", Level: null, Limit: null), Token);

        Assert.Equal(SindriErrors.MemoryMatchLevelInvalid.Code, level.Error.Code);
        Assert.Equal(SindriErrors.MemoryNotFound.Code, unknown.Error.Code);
    }
}
