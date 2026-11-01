using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>The page sheet's seam (pages spec §3.6): a page read from the map, and a write that is approved only when nothing but these pages waits.</summary>
[Lane(TestLane.Unit)]
public sealed class PageWritesTests
{
    private const string JobId = "JOB-00000000000000000000000000000007";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly PageSave _week = new(
        [new PageDraft(Id: "schedule-week", "Schedule week", Repo: null, Surface: null, Owner: true, [new PageActionDraft(Id: null, "Submit week", Action: null, ["approval"])])], []);

    private static PageWrites Writes(FakeSindriApi api)
    {
        var url = new Uri("http://127.0.0.1:1/");
        return new PageWrites(new ProfileWriteFlow(api, () => url, new FakeTimeProvider(A.Noon)), api, () => url);
    }

    private static RouteAnswer Profile(string live, params string[] changed) =>
        new(HttpStatusCode.OK, $$"""{"approved":"h0","live":"{{live}}","changed":true,"changedPaths":[{{string.Join(',', changed.Select(c => $"\"{c}\""))}}]}""");

    private static FakeSindriApi Server(RouteAnswer profile, string state = "succeeded", string? code = null, string? summary = null)
    {
        var api = new FakeSindriApi
        {
            ProfileAnswer = profile,
            SetContextPagesAnswer = new RouteAnswer(HttpStatusCode.OK, $$"""{"accepted":{"commandId":"00000000-0000-0000-0000-000000000009","sequence":12},"jobId":"{{JobId}}"}"""),
        };
        api.JobAnswers[JobId] = new RouteAnswer(HttpStatusCode.OK, Job(state, code, summary));
        return api;
    }

    private static string Job(string state, string? code, string? summary) =>
        $$"""{"id":"{{JobId}}","kind":"SetPages","key":"k","item":null,"repo":null,"service":null,"state":"{{state}}","startedBy":"human","startedAt":"2026-10-11T12:00:00+00:00","endedAt":null,"progress":[],"rounds":0,"summary":{{Quoted(summary)}},"code":{{Quoted(code)}},"result":{"pages":[],"hash":"h1","written":true,"approved":false,"diff":[]},"cancelledBy":null,"actions":[],"cli":null}""";

    private static string Quoted(string? text) => text is null ? "null" : $"\"{text}\"";

    [Fact]
    public async Task Save_sends_the_pages_and_maps_the_approval()
    {
        var api = Server(Profile("h1", "index.pages.schedule-week"));

        var saved = await Writes(api).Save(_week, "h0", approve: true, cancellationToken: Token);

        Assert.Equal(PageSavedState.Approved, saved.State);
        var body = Assert.Single(api.PagesSet);
        Assert.Equal("h0", body.IfHash);
        var page = Assert.Single(body.Set!);
        Assert.Equal("schedule-week", page.Id);
        Assert.Equal(["approval"], Assert.Single(page.Actions).Concepts);
    }

    [Fact]
    public async Task Save_leaves_it_waiting_when_another_page_waits()
    {
        var api = Server(Profile("h1", "index.pages.schedule-week", "index.pages.other"));

        var saved = await Writes(api).Save(_week, "h0", approve: true, cancellationToken: Token);

        Assert.Equal(PageSavedState.Waiting, saved.State);
        Assert.Empty(api.ProfileApprovals);
    }

    [Fact]
    public async Task Save_and_approve_after_a_hand_edit_leaves_it_waiting()
    {
        var api = Server(new RouteAnswer(HttpStatusCode.OK, """{"approved":"h0","live":"live-old","fileHash":"h1","changed":true,"changedPaths":["index.pages.schedule-week"]}"""));

        var saved = await Writes(api).Save(_week, "file-new", approve: true, cancellationToken: Token);

        Assert.Equal(PageSavedState.Waiting, saved.State);
        Assert.Empty(api.ProfileApprovals);
    }

    [Fact]
    public async Task Save_maps_a_refusal_to_its_sentence()
    {
        var api = Server(Profile("h1"));
        api.SetContextPagesAnswer = RouteAnswer.Problem(HttpStatusCode.BadRequest, "SND-CONTEXT-252", "'aproval' is no concept");

        var saved = await Writes(api).Save(_week, "h0", approve: true, cancellationToken: Token);

        Assert.Equal(PageSavedState.Refused, saved.State);
        Assert.Equal("'aproval' is no concept", saved.Message);
    }

    [Fact]
    public async Task Page_reads_an_owner_page_with_its_action_ids()
    {
        var api = new FakeSindriApi { ContextPagesAnswer = _ => Pages(truncated: false) };

        var page = await Writes(api).Page("surface:route/GET:/schedule/week", "approval", Token);

        Assert.NotNull(page);
        Assert.Equal("schedule-week", page.Id);
        Assert.True(page.Owner);
        Assert.Equal("surface:route/GET:/schedule/week", page.Surface);
        var action = Assert.Single(page.Actions);
        Assert.Equal("submit-week", action.Id);
        Assert.Equal("action:WeekPage.submit", action.Action);
        Assert.Equal(["approval"], action.Concepts);
        Assert.Equal("approval", Assert.Single(api.ContextPagesAsked).Concept);
    }

    [Fact]
    public async Task Page_drops_the_owner_surface_key()
    {
        var api = new FakeSindriApi { ContextPagesAnswer = _ => Pages(truncated: false) };

        var page = await Writes(api).Page("surface:owner-help", concept: null, Token);

        Assert.NotNull(page);
        Assert.Null(page.Surface);
        Assert.Null(Assert.Single(page.Actions).Action);
    }

    [Fact]
    public async Task Page_reads_on_until_the_page_is_found()
    {
        var api = new FakeSindriApi { ContextPagesAnswer = q => q.Offset == 0 ? Pages(truncated: true) : Pages(truncated: false) };

        var page = await Writes(api).Page("surface:owner-help", concept: null, Token);

        Assert.NotNull(page);
        Assert.Equal(0, api.ContextPagesAsked[0].Offset);
    }

    [Fact]
    public async Task Page_is_null_when_the_map_has_no_such_page()
    {
        var api = new FakeSindriApi { ContextPagesAnswer = _ => Pages(truncated: false) };

        Assert.Null(await Writes(api).Page("surface:route/GET:/nope", concept: null, Token));
    }

    private static string Row(string key, string mark, string? concept) =>
        $"{{'id':null,'key':'{key}','name':'{key}','repo':null,'mark':'{mark}','code':false,'actions':[{{'id':null,'key':'action:{key}','name':'Go','mark':'{mark}','code':false,'concepts':[{(concept is null ? "" : $"'{concept}'")}]}}]}}";

    private static RouteAnswer Rows(bool truncated, params string[] rows) =>
        new(HttpStatusCode.OK, ("{'pages':[" + string.Join(',', rows) + "],'total':" + rows.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",'truncated':" + (truncated ? "true" : "false") + ",'mapVersion':'v41'}").Replace('\'', '"'));

    [Fact]
    public async Task Suggested_lists_only_the_sections_pages_in_its_order()
    {
        var api = new FakeSindriApi
        {
            ContextPagesAnswer = _ => Rows(truncated: false, Row("k1", "suggested", concept: null), Row("k2", "suggested", concept: "approval"), Row("k3", "suggested", concept: null), Row("k4", "owner", concept: null)),
        };

        var drafts = await Writes(api).Suggested(concept: null, ["k3", "k1"], Token);

        Assert.NotNull(drafts);
        Assert.Equal(["k3", "k1"], [.. drafts.Select(d => d.Name)], StringComparer.Ordinal);
    }

    [Fact]
    public async Task Suggested_stops_reading_once_every_key_is_found()
    {
        var api = new FakeSindriApi
        {
            ContextPagesAnswer = q => q.Offset == 0 ? Rows(truncated: true, Row("k1", "suggested", concept: null), Row("k2", "suggested", concept: null)) : Rows(truncated: false, Row("k3", "suggested", concept: null)),
        };

        var drafts = await Writes(api).Suggested(concept: null, ["k1", "k2"], Token);

        Assert.Equal(2, drafts?.Count);
        _ = Assert.Single(api.ContextPagesAsked);
    }

    [Fact]
    public async Task Suggested_asks_for_suggested_pages_only()
    {
        var api = new FakeSindriApi { ContextPagesAnswer = _ => Rows(truncated: false, Row("k1", "suggested", concept: null)) };

        _ = await Writes(api).Suggested("approval", ["k1"], Token);

        var asked = Assert.Single(api.ContextPagesAsked);
        Assert.Equal("suggested", asked.Marks);
        Assert.Equal("approval", asked.Concept);
    }

    [Fact]
    public async Task Suggested_is_null_when_the_read_fails()
    {
        var api = new FakeSindriApi();

        Assert.Null(await Writes(api).Suggested(concept: null, ["k1"], Token));
    }

    [Theory]
    [InlineData("index.pages.schedule-week", true)]
    [InlineData("repos.web.index.pages.schedule-week", true)]
    [InlineData("index.pages.other", false)]
    [InlineData("index.concepts.approval", false)]
    public void OnlyPages_holds_when_every_waiting_path_is_one_of_these_pages(string path, bool only) =>
        Assert.Equal(only, new ProfileWaiting("h1", [path]).OnlyPages(["schedule-week"]));

    [Fact]
    public void OnlyPages_holds_when_nothing_waits() => Assert.True(new ProfileWaiting("h1", []).OnlyPages(["schedule-week"]));

    [Theory]
    [InlineData("index.pages.schedule-week", "schedule-week", true)]
    [InlineData("repos.web.index.pages.schedule-week", "schedule-week", true)]
    [InlineData("index.pages.schedule-week", "week", false)]
    public void Waits_names_the_page_whose_path_waits(string path, string page, bool waits) =>
        Assert.Equal(waits, new ProfileWaiting("h1", [path]).Waits(page));

    private static RouteAnswer Pages(bool truncated) => new(HttpStatusCode.OK, string.Concat(
        "{'pages':[{'id':'schedule-week','key':'surface:route/GET:/schedule/week','name':'Schedule week','repo':'web','mark':'owner','code':true,",
        "'actions':[{'id':'submit-week','key':'action:WeekPage.submit','name':'Submit week','mark':'owner','code':false,'concepts':['approval']}]},",
        "{'id':'help','key':'surface:owner-help','name':'Help','repo':null,'mark':'owner','code':false,",
        "'actions':[{'id':'ask','key':'action:owner/help/ask','name':'Ask','mark':'owner','code':false,'concepts':[]}]}],'total':2,'truncated':",
        truncated ? "true" : "false",
        ",'mapVersion':'v41'}").Replace('\'', '"'));

    [Fact]
    public void A_new_page_is_mine_when_its_path_was_not_already_waiting()
    {
        var save = new PageSave([new PageDraft(Id: null, "Time off", Repo: null, Surface: null, Owner: false, [])], []) { Before = ["index.pages.old"] };
        var mine = PageWrites.Mine(save);

        Expect.True(new ProfileWaiting("h", ["index.pages.old", "index.pages.time-off"]).OnlyMine(path => mine(path) || path == "index.pages.old") && mine("index.pages.time-off"), "the new path is the write's");
        Expect.True(!mine("index.pages.old"), "a path that already waited is not the write's");
        Expect.True(!new ProfileWaiting("h", ["index.pages.old", "index.pages.time-off"]).OnlyMine(mine), "so the write stays Waiting while that other path is still there");
        Expect.True(new ProfileWaiting("h", ["index.pages.time-off-2"]).OnlyMine(mine), "alone, the new path approves");
    }
}
