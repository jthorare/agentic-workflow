using Aw.Kernel.Testing;

namespace Aw.Sindri.Ui.Tests.Memory;

/// <summary>The Memory page's actions while one is in flight: a second call does nothing and the button is off; a forget beats a search that was already on its way.</summary>
[Lane(TestLane.Unit)]
public sealed class MemoryBusyTests
{
    private readonly MemoryFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private TaskCompletionSource Hold()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _f.Server.MemoryGate = gate;
        return gate;
    }

    [Fact]
    public async Task A_second_create_while_one_is_in_flight_sends_nothing_and_the_button_is_off()
    {
        var page = await _f.Shown(Token);
        page.NewName = "runbooks";
        var gate = Hold();

        var first = page.CreateStore(Token);
        await page.CreateStore(Token);

        Assert.False(page.CanCreate);
        _ = Assert.Single(_f.Server.CreatedMemories);
        gate.SetResult();
        await first;
        Assert.True(page.CanCreate);
        Assert.False(page.HasCreateError);
        Assert.Equal("runbooks,team-notes", string.Join(',', page.Stores.Select(s => s.Name)));
    }

    [Fact]
    public async Task A_second_write_while_one_is_in_flight_sends_nothing_and_the_button_is_off()
    {
        var page = await _f.Shown(Token);
        var store = Assert.Single(page.Stores);
        store.WriteText = "Standup is at ten";
        var gate = Hold();

        var first = store.Write(Token);
        await store.Write(Token);

        Assert.False(store.CanWrite);
        _ = Assert.Single(_f.Server.WrittenFacts);
        gate.SetResult();
        await first;
        Assert.True(store.CanWrite);
        Assert.Equal(("Added.", "5 facts"), (store.WriteResult, store.FactsText));
    }

    [Fact]
    public async Task A_second_forget_of_the_same_fact_while_one_is_in_flight_sends_nothing_and_keeps_Forgot_1()
    {
        var page = await _f.Shown(Token);
        var store = await MemoryFixture.Searched(page, "Deploys", Token);
        var row = store.Search.Rows[0];
        var gate = Hold();

        var first = row.Forget(Token);
        await row.Forget(Token);

        Assert.False(store.CanForget);
        Assert.False(row.CanForget);
        _ = Assert.Single(_f.Server.Forgets);
        gate.SetResult();
        await first;
        Assert.True(store.CanForget);
        Assert.Equal("Forgot 1 fact.", store.ForgetResult);
    }

    [Fact]
    public async Task A_forget_of_a_tag_or_the_expired_while_a_forget_is_in_flight_sends_nothing()
    {
        var page = await _f.Shown(Token);
        var store = await MemoryFixture.Searched(page, "Deploys", Token);
        var gate = Hold();

        var first = store.Search.Rows[0].Forget(Token);
        await store.Search.Rows[1].Tags[0].Forget(Token);
        await store.ForgetExpired(Token);

        _ = Assert.Single(_f.Server.Forgets);
        gate.SetResult();
        await first;
        Assert.Equal("Forgot 1 fact.", store.ForgetResult);
    }

    [Fact]
    public async Task A_search_in_flight_when_a_forget_succeeds_does_not_bring_the_forgotten_fact_back()
    {
        var page = await _f.Shown(Token);
        var store = await MemoryFixture.Searched(page, "Deploys", Token);
        var gate = Hold();
        var search = store.Search.Run(Token);   // its answer holds f1 and f2
        _f.Server.MemoryGate = null;

        await store.Search.Rows[0].Forget(Token);
        gate.SetResult();
        await search;

        Assert.Equal("f2", string.Join(',', store.Search.Rows.Select(r => r.Key)));
        Assert.Equal("1 fact found", store.Search.ResultText);
    }

    [Fact]
    public async Task A_past_expiry_is_refused_before_anything_is_sent()
    {
        var page = await _f.Shown(Token);
        var store = Assert.Single(page.Stores);
        (store.WriteText, store.WriteExpires) = ("A fact", "2020-01-01");

        await store.Write(Token);

        Assert.Empty(_f.Server.WrittenFacts);
        Assert.Equal("That expiry has already passed. Pick a later date.", store.WriteResult);
        Assert.Equal("A fact", store.WriteText);
    }
}
