using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Memory;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Memory;

[Lane(TestLane.Unit)]
public sealed class WriteMemoryFactTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_near_duplicate_with_the_same_tags_refreshes_instead_of_adding()
    {
        var h = new MemoryHarness();
        await h.Memory();
        _ = h.Embedder.With("Deploys run from main", 1f, 0.2f, 0f, 0f).With("The deploy runs from the main branch", 1f, 0.25f, 0.02f, 0f);
        var first = await h.Write().Handle(MemoryHarness.Fact("Deploys run from main", "ops"), Token);
        h.Clock.Advance(TimeSpan.FromHours(3));

        var second = await h.Write().Handle(MemoryHarness.Fact("The deploy runs from the main branch", "ops"), Token);

        Assert.Equal("added", first.Value.Action);
        Assert.Equal("refreshed", second.Value.Action);
        Assert.Equal(first.Value.Key, second.Value.Key);
        var only = Assert.Single(h.Store.AllFacts);
        Assert.Equal(h.Clock.GetUtcNow(), only.RefreshedAt);
    }

    [Fact]
    public async Task A_launched_session_writing_a_fact_is_its_writer_whatever_the_request_says()
    {
        var h = new MemoryHarness();
        await h.Memory();
        var caller = A.Caller().Launched("LCH-0000000000000000000000000000ff01").Build();
        using var entered = h.Callers.Enter(caller);
        var request = MemoryHarness.Fact("Deploys run from main") with { Provenance = new MemoryProvenance(Workflow: null, Run: null, Item: null, Session: "session:launch:someone-else") };

        var written = await h.Write().Handle(request, Token);

        Assert.True(written.IsSuccess);
        var stored = Assert.Single(h.Store.AllFacts);
        Assert.Equal(caller.Actor, stored.WrittenBy);
        Assert.Contains("LCH-0000000000000000000000000000ff01", stored.WrittenBy, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_same_text_with_different_tags_is_a_new_fact()
    {
        var h = new MemoryHarness();
        await h.Memory();
        _ = await h.Write().Handle(MemoryHarness.Fact("Deploys run from main", "ops"), Token);

        var second = await h.Write().Handle(MemoryHarness.Fact("Deploys run from main", "dev"), Token);

        Assert.Equal("added", second.Value.Action);
        Assert.Equal(2, h.Store.AllFacts.Count);
    }

    [Fact]
    public async Task A_fact_over_2000_characters_is_refused_and_nothing_is_stored()
    {
        var h = new MemoryHarness();
        await h.Memory();

        var result = await h.Write().Handle(MemoryHarness.Fact(new string('x', MemoryFact.MaxTextLength + 1)), Token);

        Assert.Equal(SindriErrors.MemoryFactInvalid.Code, result.Error.Code);
        Assert.Empty(h.Store.AllFacts);
    }

    [Fact]
    public async Task Without_the_model_the_fact_is_stored_with_no_vector()
    {
        var h = new MemoryHarness();
        await h.Memory();
        h.Embedder.Off = true;

        var result = await h.Write().Handle(MemoryHarness.Fact("Deploys run from main"), Token);

        Assert.Equal("added", result.Value.Action);
        Assert.True(Assert.Single(h.Store.AllFacts).Embedding.IsEmpty);
    }

    [Fact]
    public async Task A_write_to_an_unknown_memory_is_not_found()
    {
        var h = new MemoryHarness();

        var result = await h.Write().Handle(MemoryHarness.Fact("anything"), Token);

        Assert.Equal(SindriErrors.MemoryNotFound.Code, result.Error.Code);
    }
}
