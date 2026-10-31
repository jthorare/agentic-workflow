using Aw.Context.Core;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.SetPages;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.SetPages;

/// <summary>The SetPages job (pages spec §3.1, "The write"): it writes pages into 'index.pages', returns the new hash and never approves; every refusal comes before the write.</summary>
[Lane(TestLane.Unit)]
public sealed class SetPagesServiceTests : IDisposable
{
    private const string Surface = "surface:route/GET:/schedule/week";

    private readonly SetPagesRig _rig = new();

    public void Dispose() => _rig.Dispose();

    private static PageSettings Page(string name, string? id = null, string? repo = null, string? surface = null, PageActionSettings[]? actions = null) =>
        new(id is null ? null : PagePinId.From(id), name, repo, surface, actions);

    private static PageActionSettings Action(string name, string? key = null, string[]? concepts = null) => new(Name: name, Action: key, Concepts: concepts);

    private async Task<Kernel.Result<SetPagesResult>> Run(SetPagesRequest request)
    {
        await _rig.Lease();
        return await _rig.Service().Handle(request, SetPagesRig.Token);
    }

    private static void Refused(Kernel.Result<SetPagesResult> done, string code, string text)
    {
        Assert.True(done.IsError);
        Assert.Equal(code, done.Error.Code);
        Assert.Contains(text, done.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Page_set_replaces_the_page_and_waits_for_approval()
    {
        _rig.Profile(
            [new PageSettings(PagePinId.From("schedule-week"), "Schedule wk", Actions: [new PageActionSettings(PageActionPinId.From("old"), "Old")])],
            new RepoEntry("web", "/work/web"));
        var page = new PageSettings(
            PagePinId.From("schedule-week"),
            "Schedule week",
            Surface: "surface:route/GET:/schedule/week",
            Actions: [new PageActionSettings(Name: "Submit a schedule", Action: "action:SchedulePage.submit-schedule", Concepts: ["self-scheduling"])]);

        var done = await Run(new SetPagesRequest(Set: [page]));

        Assert.True(done.IsSuccess);
        var edit = Assert.Single(_rig.Edits);
        Assert.Equal(PagePinId.From("schedule-week"), edit.Id);
        Assert.Null(edit.Repo);
        Assert.Equal("Schedule week", edit.Entry?.Name);
        var action = Assert.Single(edit.Entry?.Actions ?? []);
        Assert.Equal(PageActionPinId.From("submit-a-schedule"), action.Id);
        Assert.Equal(["self-scheduling"], action.Concepts);
        Assert.True(done.Value.Written);
        Assert.False(done.Value.Approved);
        Assert.Equal([new PageWritten(PagePinId.From("schedule-week"), "Schedule week", Repo: null, "updated")], done.Value.Pages);
    }

    [Fact]
    public async Task A_page_naming_its_map_action_with_capitals_passes_the_write_check()
    {
        _rig.Map.Actions.Clear();
        _rig.Map.Actions["action:weekpage.submit"] = ["web"];   // the map stores the key lower-cased (issue 1790)

        var done = await Run(new SetPagesRequest(Set: [Page("Week", actions: [Action("Submit", "action:WeekPage.submit")])]));

        Assert.True(done.IsSuccess);
        Assert.Equal("action:WeekPage.submit", Assert.Single(_rig.Edits).Entry?.Actions?[0].Action);   // written as typed
    }

    [Fact]
    public async Task A_new_page_goes_top_level_with_an_id_for_the_page_and_every_action()
    {
        var done = await Run(new SetPagesRequest(Set: [Page("Schedule week", surface: Surface, actions: [Action("Submit a schedule"), Action("Submit a Schedule!")])]));

        Assert.True(done.IsSuccess);
        var edit = Assert.Single(_rig.Edits);
        Assert.Equal(PagePinId.From("schedule-week"), edit.Id);
        Assert.Equal(PagePinId.From("schedule-week"), edit.Entry?.Id);
        Assert.Null(edit.Repo);
        Assert.Equal([PageActionPinId.From("submit-a-schedule"), PageActionPinId.From("submit-a-schedule-2")], edit.Entry?.Actions?.Select(static a => a.Id!.Value));
        Assert.Equal("created", Assert.Single(done.Value.Pages).Action);
    }

    [Fact]
    public async Task An_id_given_is_kept_and_an_id_left_out_is_the_names_slug()
    {
        _ = await Run(new SetPagesRequest(Set: [Page("Schedule week", id: "week"), Page("Approvals Queue")]));

        Assert.Equal([PagePinId.From("week"), PagePinId.From("approvals-queue")], _rig.Edits.Select(static e => e.Id));
    }

    [Fact]
    public async Task A_page_under_a_repos_own_pages_stays_there()
    {
        _rig.Profile([], new RepoEntry("web", "/work/web", new IndexSettings(Pages: [Page("Schedule week", id: "schedule-week")])), new RepoEntry("admin", "/work/admin"));

        var done = await Run(new SetPagesRequest(Set: [Page("Week", id: "schedule-week")]));

        Assert.True(done.IsSuccess);
        Assert.Equal("web", Assert.Single(_rig.Edits).Repo);
        Assert.Equal("web", Assert.Single(done.Value.Pages).Repo);
    }

    [Fact]
    public async Task A_repo_other_than_the_one_holding_the_page_is_252()
    {
        _rig.Profile([], new RepoEntry("web", "/work/web", new IndexSettings(Pages: [Page("Schedule week", id: "schedule-week")])), new RepoEntry("admin", "/work/admin"));

        var done = await Run(new SetPagesRequest(Set: [Page("Week", id: "schedule-week", repo: "admin")]));

        Refused(done, ContextErrors.PageRepoAmbiguous.Code, "the page's repo is 'web'");
        Assert.Empty(_rig.Writer.Writes);
    }

    [Fact]
    public async Task Several_repos_and_no_repo_and_no_surface_is_252()
    {
        _rig.Profile([], new RepoEntry("web", "/work/web"), new RepoEntry("admin", "/work/admin"));

        var done = await Run(new SetPagesRequest(Set: [Page("Week")]));

        Refused(done, ContextErrors.PageRepoAmbiguous.Code, "say which repo this page is in, admin or web (fix: --repo)");
    }

    [Fact]
    public async Task Several_repos_with_a_surface_in_one_of_them_passes()
    {
        _rig.Profile([], new RepoEntry("web", "/work/web"), new RepoEntry("admin", "/work/admin"));

        Assert.True((await Run(new SetPagesRequest(Set: [Page("Week", surface: Surface)]))).IsSuccess);
    }

    [Fact]
    public async Task A_surface_in_two_repos_needs_one_named()
    {
        _rig.Profile([], new RepoEntry("web", "/work/web"), new RepoEntry("admin", "/work/admin"));
        _rig.Map.Surfaces[Surface] = ["web", "admin"];

        Refused(await Run(new SetPagesRequest(Set: [Page("Week", surface: Surface)])), ContextErrors.PageRepoAmbiguous.Code, "web and admin");
        Assert.True((await Run(new SetPagesRequest(Set: [Page("Week", surface: Surface, repo: "admin")]))).IsSuccess);
    }

    [Fact]
    public async Task An_unknown_repo_is_252_with_the_repos_there_are()
    {
        var done = await Run(new SetPagesRequest(Set: [Page("Week", repo: "wbe")]));

        Refused(done, ContextErrors.PageRepoAmbiguous.Code, "no repo 'wbe' (fix: one of web)");
    }

    [Fact]
    public async Task A_concept_by_its_exact_name_ignoring_case_becomes_its_id()
    {
        _ = await Run(new SetPagesRequest(Set: [Page("Week", actions: [Action("Submit", concepts: ["approval WORKFLOW", "self-scheduling"])])]));

        Assert.Equal(["approval-workflow", "self-scheduling"], Assert.Single(_rig.Edits).Entry?.Actions?[0].Concepts);
    }

    [Fact]
    public async Task A_name_matching_a_pin_and_a_derived_concept_resolves_to_the_pin()
    {
        _rig.Map.Concepts.Add(new MapConcept("billing-derived", "Billing"));
        _rig.Profile([], new RepoEntry("web", "/work/web", new IndexSettings(Concepts: [new ConceptSettings(ConceptPinId.From("billing"), "Billing")])));

        var done = await Run(new SetPagesRequest(Set: [Page("Week", actions: [Action("Pay", concepts: ["Billing"])])]));

        Assert.True(done.IsSuccess);
        Assert.Equal(["billing"], Assert.Single(_rig.Edits).Entry?.Actions?[0].Concepts);
    }

    [Fact]
    public async Task A_pin_the_map_has_not_built_yet_is_still_a_concept()
    {
        _rig.Profile([], new RepoEntry("web", "/work/web", new IndexSettings(Concepts: [new ConceptSettings(ConceptPinId.From("refunds"), "Refunds")])));

        var done = await Run(new SetPagesRequest(Set: [Page("Week", actions: [Action("Pay", concepts: ["refunds", "concept:approval-workflow"])])]));

        Assert.True(done.IsSuccess);
        Assert.Equal(["refunds", "concept:approval-workflow"], Assert.Single(_rig.Edits).Entry?.Actions?[0].Concepts);
    }

    [Fact]
    public async Task An_unknown_concept_is_250_with_the_nearest()
    {
        var done = await Run(new SetPagesRequest(Set: [Page("Week", actions: [Action("Submit", concepts: ["approval-workflo"])])]));

        Refused(done, ContextErrors.PageConceptUnknown.Code, "(fix: try approval-workflow)");
        Assert.Empty(_rig.Writer.Writes);
    }

    [Fact]
    public async Task An_unknown_surface_is_251()
    {
        var done = await Run(new SetPagesRequest(Set: [Page("Week", surface: "surface:route/GET:/schedule/wk")]));

        Refused(done, ContextErrors.PageKeyUnknown.Code, "is not in the current map");
    }

    [Fact]
    public async Task An_unknown_action_key_is_251()
    {
        var done = await Run(new SetPagesRequest(Set: [Page("Week", actions: [Action("Submit", "action:Nope.nothing")])]));

        Refused(done, ContextErrors.PageKeyUnknown.Code, "actions[0].action");
    }

    [Fact]
    public async Task Removing_a_page_writes_a_removal_where_the_page_is()
    {
        _rig.Profile([Page("Week", id: "week")], new RepoEntry("web", "/work/web", new IndexSettings(Pages: [Page("Queue", id: "queue")])));

        var done = await Run(new SetPagesRequest(Remove: [PagePinId.From("week"), PagePinId.From("queue")]));

        Assert.True(done.IsSuccess);
        Assert.Equal([(PagePinId.From("week"), null), (PagePinId.From("queue"), "web")], _rig.Edits.Select(static e => (e.Id, e.Repo)));
        Assert.All(_rig.Edits, static e => Assert.Null(e.Entry));
        Assert.Equal([new PageWritten(PagePinId.From("week"), "Week", Repo: null, "removed"), new PageWritten(PagePinId.From("queue"), "Queue", "web", "removed")], done.Value.Pages);
    }

    [Fact]
    public async Task Removing_an_unknown_page_is_257_with_a_fix_hint()
    {
        _rig.Profile([Page("Schedule week", id: "schedule-week")], new RepoEntry("web", "/work/web"));

        var done = await Run(new SetPagesRequest(Remove: [PagePinId.From("schedul-week")]));

        Refused(done, ContextErrors.PageNotFound.Code, "no page is named 'schedul-week' (fix: try schedule-week)");
    }

    [Fact]
    public async Task A_set_hitting_a_page_with_no_id_names_it_and_asks_for_an_id()
    {
        _rig.Profile([Page("Schedule week")], new RepoEntry("web", "/work/web"));

        var done = await Run(new SetPagesRequest(Set: [Page("Schedule week")]));

        Refused(done, ContextErrors.PageIdDuplicate.Code, "index.pages[1].id: 'schedule-week' is already index.pages[0]'s, made from its name (fix: give index.pages[0] an id: in the profile)");
        Assert.Empty(_rig.Writer.Writes);
    }

    [Fact]
    public async Task A_remove_hitting_a_page_with_no_id_asks_for_an_id()
    {
        _rig.Profile([Page("Schedule week")], new RepoEntry("web", "/work/web"));

        var done = await Run(new SetPagesRequest(Remove: [PagePinId.From("schedule-week")]));

        Refused(done, ContextErrors.PageIdDuplicate.Code, "index.pages[0] has no id");
    }

    [Fact]
    public async Task An_empty_request_is_256() =>
        Refused(await Run(new SetPagesRequest()), ContextErrors.PageActionSpecInvalid.Code, "nothing to set or remove");

    [Fact]
    public async Task The_same_id_twice_or_set_and_removed_is_254()
    {
        Refused(await Run(new SetPagesRequest(Set: [Page("Week"), Page("Another", id: "week")])), ContextErrors.PageIdDuplicate.Code, "index.pages[1].id: 'week' is already index.pages[0]'s");
        Refused(await Run(new SetPagesRequest(Set: [Page("Week")], Remove: [PagePinId.From("week")])), ContextErrors.PageIdDuplicate.Code, "set and removed");
        Assert.Empty(_rig.Writer.Writes);
    }

    [Fact]
    public async Task A_name_over_80_characters_is_255() =>
        Refused(await Run(new SetPagesRequest(Set: [Page(new string('x', 81))])), ContextErrors.PageNameInvalid.Code, "81 characters; at most 80");

    [Fact]
    public async Task A_dry_run_with_the_wrong_repo_is_refused_like_a_write()
    {
        _rig.Profile([], new RepoEntry("web", "/work/web", new IndexSettings(Pages: [Page("Schedule week", id: "schedule-week")])), new RepoEntry("admin", "/work/admin"));

        var done = await Run(new SetPagesRequest(Set: [Page("Week", id: "schedule-week", repo: "admin")], DryRun: true));

        Refused(done, ContextErrors.PageRepoAmbiguous.Code, "the page's repo is 'web'");
    }

    [Fact]
    public async Task A_dry_run_writes_nothing_and_returns_the_rows_that_change()
    {
        var done = await Run(new SetPagesRequest(Set: [Page("Schedule week", surface: Surface)], DryRun: true));

        Assert.True(done.IsSuccess);
        Assert.False(done.Value.Written);
        Assert.False(done.Value.Approved);
        Assert.Empty(_rig.Writer.Writes);
        Assert.Equal(["index.pages.schedule-week: (none) -> Schedule week |  | surface:route/GET:/schedule/week | "], done.Value.Diff);
        Assert.Equal(_rig.Slice.Profiles.Live.Value.Hash, done.Value.Hash);
        Assert.DoesNotContain(_rig.Slice.Store.Events, static e => e.Envelope.Type == EventTypes.ContextPageWritten);
    }

    [Fact]
    public async Task A_moved_file_is_the_writers_003_unchanged_and_no_event_is_recorded()
    {
        var done = await Run(new SetPagesRequest(Set: [Page("Week")], IfHash: ProfileHashing.Of("read"u8)));

        Assert.Equal("SND-PROFILE-003", done.Error.Code);
        Assert.Empty(_rig.Writer.Writes);
        Assert.DoesNotContain(_rig.Slice.Store.Events, static e => e.Envelope.Type == EventTypes.ContextPageWritten);
    }

    [Fact]
    public async Task The_event_names_the_actor_the_ids_and_the_hash()
    {
        _rig.CallerIs(new CallerContextBuilder().Launched().Build());
        _rig.Profile([Page("Old", id: "old")], new RepoEntry("web", "/work/web"));

        _ = await Run(new SetPagesRequest(Set: [Page("Week")], Remove: [PagePinId.From("old")]));

        var recorded = Assert.Single(_rig.Slice.Store.Events, static e => e.Envelope.Type == EventTypes.ContextPageWritten);
        Assert.Equal("context.page.written:" + _rig.Writer.Hash.Value, recorded.IdempotencyKey);
        var payload = _rig.Slice.Events.Payload<ContextPageWritten>(recorded).Value;
        Assert.StartsWith("session:", payload.Actor, StringComparison.Ordinal);
        Assert.True(payload.Launch.HasValue);
        Assert.Equal(["week:created", "old:removed"], payload.Pages);
        Assert.Equal(_rig.Writer.Hash, payload.Hash);
    }

    [Fact]
    public async Task The_job_says_checking_and_then_written_to_the_profile()
    {
        _ = await Run(new SetPagesRequest(Set: [Page("Week")]));

        Assert.Equal(["checking", "written to the profile"], _rig.Progress.Seen);
    }

    [Fact]
    public async Task Without_the_lease_nothing_is_written()
    {
        var done = await _rig.Service().Handle(new SetPagesRequest(Set: [Page("Week")]), SetPagesRig.Token);

        Assert.Equal("SND-API-001", done.Error.Code);
        Assert.Empty(_rig.Writer.Writes);
    }

    [Fact]
    public async Task The_admission_refuses_what_the_job_would_so_the_start_is_a_real_error()
    {
        var wrong = await _rig.Admission().Admit(new SetPagesRequest(Set: [Page("Week", surface: "surface:route/GET:/nope")]), SetPagesRig.Token);
        var fine = await _rig.Admission().Admit(new SetPagesRequest(Set: [Page("Week", surface: Surface)]), SetPagesRig.Token);

        Assert.Equal(ContextErrors.PageKeyUnknown.Code, wrong.Error.Code);
        Assert.True(fine.IsSuccess);
        Assert.Empty(_rig.Writer.Writes);
    }
}
