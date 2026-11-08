using Aw.Kernel.Testing;
using Aw.Sindri.Features.ForgetMemoryFact;
using Aw.Sindri.Testing.Memory;

namespace Aw.Sindri.Tests.Memory;

[Lane(TestLane.Unit)]
public sealed class ForgetMemoryFactTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Forget_by_id_by_tag_and_expired()
    {
        var h = new MemoryHarness();
        await h.Memory();
        _ = await h.Store.Add(MemorySamples.Fact("a", "alpha"), Token);
        _ = await h.Store.Add(MemorySamples.Fact("b", "beta", tags: ["old"]), Token);
        _ = await h.Store.Add(MemorySamples.Fact("c", "gamma", tags: ["old"]), Token);
        _ = await h.Store.Add(MemorySamples.Fact("d", "delta", expiresAt: h.Clock.GetUtcNow().AddMinutes(-1)), Token);
        _ = await h.Store.Add(MemorySamples.Fact("e", "epsilon"), Token);

        var byKey = await h.Forget().Handle(new ForgetMemoryFactRequest("team-notes", "a", null, false), Token);
        var byTag = await h.Forget().Handle(new ForgetMemoryFactRequest("team-notes", null, "old", false), Token);
        var expired = await h.Forget().Handle(new ForgetMemoryFactRequest("team-notes", null, null, true), Token);

        Assert.Equal(1, byKey.Value.Deleted);
        Assert.Equal(2, byTag.Value.Deleted);
        Assert.Equal(1, expired.Value.Deleted);
        Assert.Equal(["e"], h.Store.AllFacts.Select(static f => f.Key));
    }

    [Fact]
    public async Task No_selector_or_two_selectors_delete_nothing()
    {
        var h = new MemoryHarness();
        await h.Memory();
        _ = await h.Store.Add(MemorySamples.Fact("a", "alpha", tags: ["old"]), Token);

        var none = await h.Forget().Handle(new ForgetMemoryFactRequest("team-notes", null, null, false), Token);
        var two = await h.Forget().Handle(new ForgetMemoryFactRequest("team-notes", "a", "old", false), Token);

        Assert.True(none.IsError);
        Assert.True(two.IsError);
        Assert.Single(h.Store.AllFacts);
    }
}
