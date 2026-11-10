using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.ForgetMemoryFact;
using Aw.Sindri.Testing;
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

        var byKey = await h.Forget().Handle(new ForgetMemoryFactRequest("team-notes", Fact: "a", Tag: null, Expired: false), Token);
        var byTag = await h.Forget().Handle(new ForgetMemoryFactRequest("team-notes", Fact: null, Tag: "old", Expired: false), Token);
        var expired = await h.Forget().Handle(new ForgetMemoryFactRequest("team-notes", Fact: null, Tag: null, Expired: true), Token);

        Assert.Equal(1, byKey.Value.Deleted);
        Assert.Equal(2, byTag.Value.Deleted);
        Assert.Equal(1, expired.Value.Deleted);
        Assert.Equal(["e"], h.Store.AllFacts.Select(static f => f.Key), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Forgetting_facts_appends_an_event_carrying_the_callers_actor()
    {
        var h = new MemoryHarness();
        await h.Memory();
        await h.Hold();
        _ = await h.Store.Add(MemorySamples.Fact("a", "alpha", tags: ["old"]), Token);
        _ = await h.Store.Add(MemorySamples.Fact("b", "beta", tags: ["old"]), Token);
        var caller = A.Caller().Launched("LCH-0000000000000000000000000000ff01").Build();
        using var entered = h.Callers.Enter(caller);

        var forgot = await h.Forget().Handle(new ForgetMemoryFactRequest("team-notes", Fact: null, Tag: "old", Expired: false), Token);

        Assert.Equal(2, forgot.Value.Deleted);
        var recorded = Assert.Single(h.Ledger.Events, static e => e.Envelope.Type == EventTypes.MemoryFactsForgotten);
        Assert.Equal(caller.Actor, recorded.Envelope.Actor);
    }

    [Fact]
    public async Task A_forget_that_deletes_nothing_appends_no_event()
    {
        var h = new MemoryHarness();
        await h.Memory();
        await h.Hold();

        _ = await h.Forget().Handle(new ForgetMemoryFactRequest("team-notes", Fact: "nothing", Tag: null, Expired: false), Token);

        Assert.DoesNotContain(h.Ledger.Events, static e => e.Envelope.Type == EventTypes.MemoryFactsForgotten);
    }

    [Fact]
    public async Task No_selector_or_two_selectors_delete_nothing()
    {
        var h = new MemoryHarness();
        await h.Memory();
        _ = await h.Store.Add(MemorySamples.Fact("a", "alpha", tags: ["old"]), Token);

        var none = await h.Forget().Handle(new ForgetMemoryFactRequest("team-notes", Fact: null, Tag: null, Expired: false), Token);
        var two = await h.Forget().Handle(new ForgetMemoryFactRequest("team-notes", Fact: "a", Tag: "old", Expired: false), Token);

        Assert.True(none.IsError);
        Assert.True(two.IsError);
        _ = Assert.Single(h.Store.AllFacts);
    }
}
