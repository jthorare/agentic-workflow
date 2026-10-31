using System.Net;
using Aw.Kernel.Testing;
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
        [new PageDraft("schedule-week", "Schedule week", Repo: null, Surface: null, Owner: true, [new PageActionDraft(Id: null, "Submit week", Action: null, ["approval"])])], []);

    private static PageWrites Writes(FakeSindriApi api) => new(api, () => new Uri("http://127.0.0.1:1/"), new FakeTimeProvider(A.Noon));

    private static RouteAnswer Profile(params string[] changed) =>
        new(HttpStatusCode.OK, $$"""{"approved":"h0","live":"h1","changed":true,"changedPaths":[{{string.Join(',', changed.Select(c => $"\"{c}\""))}}]}""");

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
    public async Task Save_approves_when_only_this_page_waits()
    {
        var api = Server(Profile("index.pages.schedule-week"));

        var saved = await Writes(api).Save(_week, "h0", approve: true, cancellationToken: Token);

        Assert.Equal(PageSavedState.Approved, saved.State);
        Assert.Equal("h1", Assert.Single(api.ProfileApprovals).Hash);
        Assert.Equal("h0", Assert.Single(api.PagesSet).IfHash);
    }

    [Fact]
    public async Task Save_leaves_it_waiting_when_another_change_waits()
    {
        var api = Server(Profile("index.pages.schedule-week", "index.concepts.approval"));

        var saved = await Writes(api).Save(_week, "h0", approve: true, cancellationToken: Token);

        Assert.Equal(PageSavedState.Waiting, saved.State);
        Assert.Empty(api.ProfileApprovals);
    }

    [Fact]
    public async Task Save_without_approve_never_approves()
    {
        var api = Server(Profile("index.pages.schedule-week"));

        var saved = await Writes(api).Save(_week, "h0", approve: false, cancellationToken: Token);

        Assert.Equal(PageSavedState.Waiting, saved.State);
        Assert.Empty(api.ProfileApprovals);
    }

    [Fact]
    public async Task Save_reports_a_clash_and_writes_nothing_else()
    {
        var api = Server(Profile());
        api.SetContextPagesAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-PROFILE-003", "The profile moved.");

        var saved = await Writes(api).Save(_week, "h0", approve: true, cancellationToken: Token);

        Assert.Equal(PageSavedState.Clash, saved.State);
        Assert.Equal(0, api.JobCalls);
        Assert.Empty(api.ProfileApprovals);
    }

    [Fact]
    public async Task Save_reports_a_refused_concept_with_the_servers_sentence()
    {
        var api = Server(Profile());
        const string Sentence = "index.pages[0].actions[0].concepts[0]: 'aproval' is no concept in map v41";
        api.SetContextPagesAnswer = RouteAnswer.Problem(HttpStatusCode.BadRequest, "SND-CONTEXT-252", Sentence);

        var saved = await Writes(api).Save(_week, "h0", approve: true, cancellationToken: Token);

        Assert.Equal(PageSavedState.Refused, saved.State);
        Assert.Equal("SND-CONTEXT-252", saved.Code);
        Assert.Equal(Sentence, saved.Message);
    }

    [Fact]
    public async Task Save_reports_a_failed_job_with_its_code()
    {
        var api = Server(Profile(), state: "failed", code: "SND-CONTEXT-254", summary: "no such concept");

        var saved = await Writes(api).Save(_week, "h0", approve: true, cancellationToken: Token);

        Assert.Equal(PageSavedState.Refused, saved.State);
        Assert.Equal("no such concept", saved.Message);
    }

    [Fact]
    public async Task Save_sends_the_write_once_and_polls_while_the_job_runs()
    {
        var clock = new FakeTimeProvider(A.Noon);
        var api = Server(Profile("index.pages.schedule-week"), state: "running");
        var save = Writes(api, clock).Save(_week, "h0", approve: false, cancellationToken: Token);

        while (api.JobCalls < 2)
        {
            clock.Advance(TimeSpan.FromMilliseconds(250));
            await Task.Yield();
        }

        api.JobAnswers[JobId] = new RouteAnswer(HttpStatusCode.OK, Job("succeeded", code: null, summary: null));
        while (!save.IsCompleted)
        {
            clock.Advance(TimeSpan.FromMilliseconds(250));
            await Task.Yield();
        }

        Assert.Equal(PageSavedState.Waiting, (await save).State);
        _ = Assert.Single(api.PagesSet);
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

    [Fact]
    public async Task Waiting_reads_the_live_hash_and_changed_paths()
    {
        var waiting = await Writes(new FakeSindriApi { ProfileAnswer = Profile("index.pages.x") }).Waiting(Token);

        Assert.Equal(new ProfileWaiting("h1", ["index.pages.x"]), waiting, new WaitingComparer());
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

    private static PageWrites Writes(FakeSindriApi api, TimeProvider clock) => new(api, () => new Uri("http://127.0.0.1:1/"), clock);

    private static RouteAnswer Pages(bool truncated) => new(HttpStatusCode.OK, string.Concat(
        "{'pages':[{'id':'schedule-week','key':'surface:route/GET:/schedule/week','name':'Schedule week','repo':'web','mark':'owner','code':true,",
        "'actions':[{'id':'submit-week','key':'action:WeekPage.submit','name':'Submit week','mark':'owner','code':false,'concepts':['approval']}]},",
        "{'id':'help','key':'surface:owner-help','name':'Help','repo':null,'mark':'owner','code':false,",
        "'actions':[{'id':'ask','key':'action:owner/help/ask','name':'Ask','mark':'owner','code':false,'concepts':[]}]}],'total':2,'truncated':",
        truncated ? "true" : "false",
        ",'mapVersion':'v41'}").Replace('\'', '"'));

    private sealed class WaitingComparer : IEqualityComparer<ProfileWaiting>
    {
        public bool Equals(ProfileWaiting? x, ProfileWaiting? y) => x is not null && y is not null && x.LiveHash == y.LiveHash && x.ChangedPaths.SequenceEqual(y.ChangedPaths, StringComparer.Ordinal);

        public int GetHashCode(ProfileWaiting obj) => obj.LiveHash?.GetHashCode(StringComparison.Ordinal) ?? 0;
    }
}
