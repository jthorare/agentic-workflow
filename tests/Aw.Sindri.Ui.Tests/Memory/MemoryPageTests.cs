using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Memory;

namespace Aw.Sindri.Ui.Tests.Memory;

/// <summary>The Memory page (API PR 18a): the stores, a search with its mode, write, create and the three forms of forget; nothing leaves a list before the API says so.</summary>
[Lane(TestLane.Unit)]
public sealed class MemoryPageTests
{
    private readonly MemoryFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_list_shows_each_store_with_its_fact_count()
    {
        var page = await _f.Shown(Token);

        var store = Assert.Single(page.Stores);
        Assert.Equal((MemoryFixture.Store, "4 facts", "1 store · 4 facts"), (store.Name, store.FactsText, page.Head));
        Assert.False(page.IsEmpty);
    }

    [Fact]
    public async Task No_stores_says_so()
    {
        var shown = new MemoryPageViewModel(new Aw.Sindri.Ui.Testing.FakeSindriApi(), _f.Clock);
        shown.SetShown(shown: true);
        await shown.Refresh(MemoryFixture.Url, Token);

        Assert.True(shown.IsEmpty);
        Assert.Empty(shown.Stores);
    }

    [Theory]
    [InlineData("semantic", null, "Semantic match")]
    [InlineData("keyword", "The embedding model is off, so recall used keywords", "Keyword match")]
    public async Task Search_shows_the_mode_and_a_forget_removes_the_row_after_the_API_confirms(string mode, string? note, string chip)
    {
        _f.Server.SearchMode = mode;
        _f.Server.SearchNote = note;
        var page = await _f.Shown(Token);
        var store = await MemoryFixture.Searched(page, "Deploys", Token);

        Assert.Equal((chip, note ?? string.Empty, note is not null, "2 facts found"), (store.Search.ModeText, store.Search.NoteText, store.Search.HasNote, store.Search.ResultText));
        Assert.Equal("f1,f2", string.Join(',', store.Search.Rows.Select(r => r.Key)));
        Assert.Equal(("Deploys go out on Tuesdays", "Score 1.00"), (store.Search.Rows[0].Text, store.Search.Rows[0].ScoreText));

        await store.Search.Rows[0].Forget(Token);

        Assert.Equal(("f1", null, false), (_f.Server.Forgets[0].FactId, _f.Server.Forgets[0].Tag, _f.Server.Forgets[0].Expired));
        Assert.Equal("f2", string.Join(',', store.Search.Rows.Select(r => r.Key)));
        Assert.Equal(("3 facts", "Forgot 1 fact."), (store.FactsText, store.ForgetResult));
    }

    [Fact]
    public async Task Forget_failure_keeps_the_row()
    {
        var page = await _f.Shown(Token);
        var store = await MemoryFixture.Searched(page, "Deploys", Token);
        _f.Server.ForgetError = new Error("SND-MEM-500", ErrorKind.Unavailable, "The store is locked.");

        await store.Search.Rows[0].Forget(Token);

        Assert.Equal("f1,f2", string.Join(',', store.Search.Rows.Select(r => r.Key)));
        Assert.Equal("4 facts", store.FactsText);
        Assert.Contains("The store is locked.", store.ForgetResult, StringComparison.Ordinal);
        Assert.True(store.HasForgetResult);
    }

    [Fact]
    public async Task A_forget_that_deletes_nothing_keeps_the_row_and_says_so()
    {
        var page = await _f.Shown(Token);
        var store = await MemoryFixture.Searched(page, "Deploys", Token);
        var first = store.Search.Rows[0];
        await first.Forget(Token);   // f1 is gone from the store now
        _ = _f.Server.SeedMemory(MemoryFixture.Store);   // the store has lost every fact behind the page's back

        await store.Search.Rows[0].Forget(Token);

        Assert.Equal("f2", string.Join(',', store.Search.Rows.Select(r => r.Key)));
        Assert.Equal("Nothing was forgotten.", store.ForgetResult);
    }

    [Fact]
    public async Task Forgetting_a_tag_removes_every_row_with_it_after_the_API_confirms()
    {
        var page = await _f.Shown(Token);
        var store = await MemoryFixture.Searched(page, "Deploys", Token);

        await store.Search.Rows[1].Tags.Single(t => t.Tag == "deploys").Forget(Token);

        Assert.Equal((null, "deploys", false), (_f.Server.Forgets[0].FactId, _f.Server.Forgets[0].Tag, _f.Server.Forgets[0].Expired));
        Assert.Empty(store.Search.Rows);
        Assert.Equal("Forgot 2 facts.", store.ForgetResult);
        Assert.Equal("2 facts", store.FactsText);
    }

    [Fact]
    public async Task Forgetting_the_expired_removes_only_the_expired_rows()
    {
        var page = await _f.Shown(Token);
        var store = await MemoryFixture.Searched(page, "the", Token);   // matches f3 and f4 by text
        Assert.Equal("f3,f4", string.Join(',', store.Search.Rows.Select(r => r.Key)));
        Assert.StartsWith("Expired ", store.Search.Rows[0].ExpiresText, StringComparison.Ordinal);
        Assert.StartsWith("Expires ", store.Search.Rows[1].ExpiresText, StringComparison.Ordinal);

        await store.ForgetExpired(Token);

        Assert.Equal((null, null, true), (_f.Server.Forgets[0].FactId, _f.Server.Forgets[0].Tag, _f.Server.Forgets[0].Expired));
        Assert.Equal("f4", string.Join(',', store.Search.Rows.Select(r => r.Key)));
    }

    [Fact]
    public async Task A_search_the_API_refuses_shows_the_error_and_no_rows()
    {
        var missing = new MemorySearchViewModel(_f.Server, () => MemoryFixture.Url, "gone", _f.Clock, (_, _) => Task.CompletedTask, (_, _) => Task.CompletedTask) { Query = "x" };

        await missing.Run(Token);

        Assert.True(missing.HasError);
        Assert.Empty(missing.Rows);
    }

    [Fact]
    public async Task Create_store_adds_it_to_the_list()
    {
        var page = await _f.Shown(Token);
        page.NewName = "runbooks";
        page.NewDescription = "How we run things";

        await page.CreateStore(Token);

        Assert.Equal("runbooks", string.Join(',', page.Stores.Select(s => s.Name)));
        Assert.Equal(("runbooks", "How we run things"), (_f.Server.CreatedMemories[0].Name, _f.Server.CreatedMemories[0].Description));
        Assert.Equal(("0 facts", "How we run things", string.Empty), (page.Stores[0].FactsText, page.Stores[0].Description, page.NewName));
    }

    [Fact]
    public async Task Create_store_keeps_the_form_and_shows_the_error_when_the_API_refuses()
    {
        var page = await _f.Shown(Token);
        _f.Server.MemoryWriteError = new Error("SND-MEM-002", ErrorKind.Conflict, "That name is taken.");
        page.NewName = "team-notes";

        await page.CreateStore(Token);

        _ = Assert.Single(page.Stores);
        Assert.Equal("team-notes", page.NewName);
        Assert.Contains("That name is taken.", page.CreateError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_store_without_a_name_sends_nothing()
    {
        var page = await _f.Shown(Token);

        await page.CreateStore(Token);

        Assert.Empty(_f.Server.CreatedMemories);
        Assert.True(page.HasCreateError);
    }

    [Fact]
    public async Task Write_fact_shows_added_or_refreshed()
    {
        var page = await _f.Shown(Token);
        var store = Assert.Single(page.Stores);
        store.WriteText = "Standup is at ten";
        store.WriteTags = "team, calendar";
        store.WriteExpires = "2100-02-03";

        await store.Write(Token);

        var sent = _f.Server.WrittenFacts[0].Body;
        Assert.Equal(("Standup is at ten", ["team", "calendar"], new DateTimeOffset(2100, 2, 3, 0, 0, 0, TimeSpan.Zero)), (sent.Text, sent.Tags, sent.ExpiresAt));
        Assert.Equal(("Added.", "5 facts", string.Empty), (store.WriteResult, store.FactsText, store.WriteText));

        store.WriteText = "Standup is at ten";
        store.WriteTags = "team, calendar";
        await store.Write(Token);

        Assert.Equal(("Refreshed: that fact was already there.", "5 facts"), (store.WriteResult, store.FactsText));
    }

    [Fact]
    public async Task Write_fact_refuses_a_bad_expiry_without_sending()
    {
        var page = await _f.Shown(Token);
        var store = Assert.Single(page.Stores);
        (store.WriteText, store.WriteExpires) = ("A fact", "next week");

        await store.Write(Token);

        Assert.Empty(_f.Server.WrittenFacts);
        Assert.Equal("The expiry isn't a date. Use yyyy-mm-dd.", store.WriteResult);
        Assert.Equal("A fact", store.WriteText);
    }

    [Fact]
    public async Task Write_fact_keeps_the_form_when_the_API_refuses()
    {
        var page = await _f.Shown(Token);
        var store = Assert.Single(page.Stores);
        _f.Server.MemoryWriteError = new Error("SND-MEM-007", ErrorKind.Invalid, "The expiry has to be in the future.");
        store.WriteText = "A fact";

        await store.Write(Token);

        Assert.Contains("The expiry has to be in the future.", store.WriteResult, StringComparison.Ordinal);
        Assert.Equal(("A fact", "4 facts"), (store.WriteText, store.FactsText));
    }
}
