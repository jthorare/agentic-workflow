using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Pages;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The pasted-pages preview: the paste is checked, the pages come from the engine under the caller's scope (all of them, 200 at a time), and the answer is the pure preview with the page to save per ticked block.</summary>
public sealed class PreviewPagesServiceTests
{
    private const string Week = "surface:route/GET:/schedule/week";

    private readonly SliceHarness _h = new();
    private readonly PagesEngineProxy _engine = PagesEngineProxy.Create();
    private readonly MapFacts _map = new();
    private ProfileHash _hash;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static PageRow Suggested(string id, string name, string key, params PageActionRow[] actions) =>
        new(PageId.From(id), NodeKey.From(key), name, Repo: "app", Mark: "suggested", Code: true, Actions: actions);

    private static PageActionRow Submit() =>
        new(PageActionId.From("submit-schedule"), NodeKey.From("action:WeekPage.submit"), "Submit schedule", "suggested", Code: true, Concepts: ["self-scheduling"]);

    private static PagesView View(List<PageRow> rows, bool truncated = false) => new(rows, rows.Count, truncated, "v13");

    private async Task<PreviewPagesService> Service(SindriProfile? live = null)
    {
        _ = await _h.Profiles.Save(SliceHarness.Profile(), Ct);
        var profile = live ?? new SindriProfile(1, "thor");
        _hash = ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json));
        _h.Profiles.Live = new LoadedProfile(profile, _hash, "/home/profile.yaml");
        _engine.Answer = View([Suggested("schedule-week", "Schedule week", Week, Submit())]);
        return new PreviewPagesService(new ContextScopes(_h.Profiles, _h.Callers), _engine.Proxy, _h.Profiles, _map);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_paste_naming_a_suggested_page_answers_the_page_to_save()
    {
        var answer = await (await Service()).Handle(new PreviewPagesRequest("Schedule week\nSubmit schedule\nPrint roster -> Approval workflow\n\nBilling"), Ct);

        Assert.Equal("v41", answer.Value.MapVersion);
        var first = answer.Value.Blocks[0];
        Assert.Equal((1, 1, true, Week), (first.Block, first.Line, first.Ticked, first.Match?.Key.Value));
        Assert.Equal((Week, "app", null), (first.Page?.Surface, first.Page?.Repo, first.Page?.Id?.Value));
        var second = answer.Value.Blocks[1];
        Assert.Equal((2, 5, true, null), (second.Block, second.Line, second.Ticked, second.Match?.Key.Value));
        Assert.Equal(("Billing", null), (second.Page?.Name, second.Page?.Surface));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_answer_carries_the_hash_of_the_live_profile_it_read()
    {
        var answer = await (await Service()).Handle(new PreviewPagesRequest("Home"), Ct);

        Assert.Equal(_hash, answer.Value.ProfileHash);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_actions_to_save_carry_the_matched_action_and_the_concept_ids()
    {
        var answer = await (await Service()).Handle(new PreviewPagesRequest("Schedule week\nSubmit schedule\nPrint roster -> Approval workflow"), Ct);

        var actions = answer.Value.Blocks[0].Page?.Actions;
        Assert.Equal(("Submit schedule", "action:WeekPage.submit", "self-scheduling"), (actions?[0].Name, actions?[0].Action, string.Join('|', actions?[0].Concepts ?? [])));
        Assert.Equal(("Print roster", null, "approval-workflow"), (actions?[1].Name, actions?[1].Action, string.Join('|', actions?[1].Concepts ?? [])));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_page_that_matched_under_another_name_keeps_the_matched_pages_id_so_the_save_replaces_the_same_page()
    {
        var answer = await (await Service()).Handle(new PreviewPagesRequest("Schedule weeks"), Ct);

        Assert.Equal("schedule-week", answer.Value.Blocks[0].Page?.Id?.Value);
        Assert.Equal("Schedule weeks", answer.Value.Blocks[0].Page?.Name);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_block_that_replaces_an_owner_page_keeps_its_actions_ids()
    {
        var owner = new PageSettings(PagePinId.From("week-page"), "Week page", Actions:
        [
            new PageActionSettings(PageActionPinId.From("send-it"), "Submit schedule", "action:WeekPage.submit"),
            new PageActionSettings(PageActionPinId.From("my-swap"), "Swap shift"),
        ]);
        var service = await Service(new SindriProfile(1, "thor", Index: new IndexSettings(Pages: [owner])));
        _engine.Answer = View([new PageRow(PageId.From("week-page"), NodeKey.From("surface:route/GET:/week"), "Week page", Repo: "app", Mark: "owner", Code: true, Actions: [])]);

        var answer = await service.Handle(new PreviewPagesRequest("Week page\nSubmit schedule\nSwap shift\nAdd note"), Ct);

        var page = answer.Value.Blocks[0].Page;
        Assert.Equal("week-page", answer.Value.Blocks[0].Replaces?.Value);
        Assert.Equal("send-it|my-swap|", string.Join('|', page?.Actions?.Select(static a => a.Id?.Value) ?? []));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_kept_action_id_does_not_collide_with_a_new_actions_slug()
    {
        var owner = new PageSettings(PagePinId.From("week-page"), "Week page", Actions: [new PageActionSettings(PageActionPinId.From("send-it"), "Submit schedule")]);
        var service = await Service(new SindriProfile(1, "thor", Index: new IndexSettings(Pages: [owner])));
        _engine.Answer = View([new PageRow(PageId.From("week-page"), NodeKey.From("surface:route/GET:/week"), "Week page", Repo: "app", Mark: "owner", Code: true, Actions: [])]);

        var answer = await service.Handle(new PreviewPagesRequest("Week page\nSubmit schedule\nSend it"), Ct);

        Assert.Equal("send-it|send-it-2", string.Join('|', answer.Value.Blocks[0].Page?.Actions?.Select(static a => a.Id?.Value ?? PageSlugs.Of(a.Name)) ?? []));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_kept_action_id_is_found_by_the_map_key_whatever_its_case_in_the_profile()
    {
        var owner = new PageSettings(PagePinId.From("week-page"), "Week page", Actions: [new PageActionSettings(PageActionPinId.From("send-it"), "Send schedule", "action:WeekPage.submit")]);
        var service = await Service(new SindriProfile(1, "thor", Index: new IndexSettings(Pages: [owner])));
        var key = NodeKey.From("surface:route/GET:/week");
        var action = new PageActionRow(PageActionId.From("submit-schedule"), NodeKey.From("action:weekpage.submit"), "Submit schedule", "suggested", Code: true, Concepts: []);
        _engine.Answer = View(
        [
            Suggested("week-page", "Week page", key.Value, action),
            new PageRow(PageId.From("week-page"), key, "Week page", Repo: "app", Mark: "owner", Code: true, Actions: []),
        ]);

        var answer = await service.Handle(new PreviewPagesRequest("Week page\nSubmit schedule"), Ct);

        Assert.Equal("send-it", answer.Value.Blocks[0].Page?.Actions?[0].Id?.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_row_that_is_not_added_has_no_page_and_says_why()
    {
        var answer = await (await Service()).Handle(new PreviewPagesRequest("Schedule week\n\nSchedule week\nPay -> aproval"), Ct);

        var second = answer.Value.Blocks[1];
        Assert.Equal((false, 1, "Same page as block 1"), (second.Ticked, second.SameAs, second.Reason));
        Assert.Null(second.Page);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_repo_of_the_request_fills_a_new_page_and_narrows_the_pages_read()
    {
        var answer = await (await Service()).Handle(new PreviewPagesRequest("Billing", "app"), Ct);

        Assert.Equal("app", answer.Value.Blocks[0].Page?.Repo);
        Assert.Equal("app", Assert.Single(_engine.Queries).Repo?.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_concept_the_live_profile_pins_resolves_before_the_map_s()
    {
        var live = new SindriProfile(1, "thor", Index: new IndexSettings(Concepts: [new ConceptSettings(ConceptPinId.From("staffing"), "Self-scheduling")]));

        var answer = await (await Service(live)).Handle(new PreviewPagesRequest("Home\nOpen -> Self-scheduling"), Ct);

        Assert.Equal("staffing", string.Join('|', answer.Value.Blocks[0].Page?.Actions?[0].Concepts ?? []));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("  \n\t \n")]
    public async Task Nothing_pasted_is_259_and_the_engine_is_not_asked(string text)
    {
        var answer = await (await Service()).Handle(new PreviewPagesRequest(text), Ct);

        Assert.Equal("SND-CONTEXT-259", answer.Error.Code);
        Assert.Equal("pages preview: nothing pasted (fix: one page per block: its name, then one action per line)", answer.Error.Message);
        Assert.Empty(_engine.Queries);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_paste_over_65536_bytes_is_259_and_one_of_exactly_65536_is_read()
    {
        var service = await Service();

        var over = await service.Handle(new PreviewPagesRequest(new string('a', 70_120)), Ct);
        var exactly = await service.Handle(new PreviewPagesRequest(new string('a', 65_536)), Ct);

        Assert.Equal(("SND-CONTEXT-259", "pages preview: 70,120 bytes; at most 65,536"), (over.Error.Code, over.Error.Message));
        Assert.True(exactly.IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_size_is_counted_in_utf8_bytes_not_characters()
    {
        var answer = await (await Service()).Handle(new PreviewPagesRequest(new string('é', 40_000)), Ct);

        Assert.Equal("pages preview: 80,000 bytes; at most 65,536", answer.Error.Message);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_that_cannot_be_read_is_258_without_asking_the_engine()
    {
        var answer = await (await Service()).Handle(new PreviewPagesRequest("Home", "No Repo!"), Ct);

        Assert.Equal("SND-CONTEXT-258", answer.Error.Code);
        Assert.Empty(_engine.Queries);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_engine_s_error_for_no_map_comes_back_as_it_is()
    {
        var service = await Service();
        _engine.Answer = ContextErrors.NoMap.With("no map yet");

        var answer = await service.Handle(new PreviewPagesRequest("Home"), Ct);

        Assert.Equal(ContextErrors.NoMap.Code, answer.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Pages_are_read_200_at_a_time_until_the_engine_says_no_more_follow()
    {
        var service = await Service();
        var filler = Enumerable.Range(0, 200).Select(static i => Suggested("p" + i.ToString(CultureInfo.InvariantCulture), "Filler " + i.ToString(CultureInfo.InvariantCulture), "surface:screen/F" + i.ToString(CultureInfo.InvariantCulture))).ToList();
        _engine.AnswerFor = query => query.Offset == 0
            ? View(filler, truncated: true)
            : View([Suggested("late", "Late page", "surface:screen/Late")]);

        var answer = await service.Handle(new PreviewPagesRequest("Late page"), Ct);

        Assert.Equal("0|200", string.Join('|', _engine.Queries.Select(static q => q.Offset)));
        Assert.All(_engine.Queries, static q => Assert.Equal(200, q.Limit));
        Assert.Equal("surface:screen/Late", answer.Value.Blocks[0].Match?.Key.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_reads_the_pages_under_its_own_repo_scope()
    {
        var service = await Service();
        using var entered = _h.Callers.Enter(new CallerContext(CallerClass.Session, Terminal: false, "session:mcp", _h.Ids.New<CommandId>(), _h.Ids.New<CorrelationId>(), RepoName.From("app")));

        _ = await service.Handle(new PreviewPagesRequest("Home"), Ct);

        var scope = Assert.Single(_engine.Scopes);
        Assert.Equal((ContextCallerKind.Session, "app"), (scope?.Caller.Kind, scope?.Caller.Repo?.Value));
    }

    private sealed class MapFacts : IMapPageFacts
    {
        public ValueTask<Result<MapPageFacts>> Read(CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<MapPageFacts>>(new MapPageFacts(
                "v41", [new MapConcept("self-scheduling", "Self-scheduling"), new MapConcept("approval-workflow", "Approval workflow")],
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal), new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)));
    }
}
