using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Pages;
using Aw.Context.Tests.Map;
using Aw.Kernel;

namespace Aw.Context.Tests.Pages;

/// <summary>The pages read over a map with suggested pages (pages spec §3.4): order, filters, errors, paging and a session's repo scope.</summary>
public sealed class GetPagesReadTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private async Task<PagesView> Read(PagesQuery query, ContextScope scope)
    {
        _ = await _world.ImportYaml(PagesWorld.Yaml());
        return (await _world.Engine.Pages(query, scope, MapWorld.None)).Value;
    }

    private async Task<Error> Refused(PagesQuery query)
    {
        _ = await _world.ImportYaml(PagesWorld.Yaml());
        return (await _world.Engine.Pages(query, PagesWorld.Human(), MapWorld.None)).Error;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_suggested_page_is_listed_with_the_slug_of_its_offered_name_and_its_surface_key()
    {
        var view = await Read(PagesWorld.Query(), PagesWorld.Human());

        var week = view.Pages[0];
        Assert.Equal(("schedule-week", PagesWorld.Week, "Schedule week", "suggested", true), (week.Id.Value, week.Key.Value, week.Name, week.Mark, week.Code));
        Assert.Equal(["cancel", "submit"], week.Actions.Select(a => a.Id.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.All(week.Actions, a => Assert.Equal("suggested", a.Mark));
        Assert.Equal(["self-scheduling"], week.Actions.Single(a => a.Id.Value == "submit").Concepts, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_route_implemented_from_a_ts_file_is_no_page()
    {
        var view = await Read(PagesWorld.Query(), PagesWorld.Human());

        Assert.DoesNotContain(view.Pages, p => p.Key.Value == "surface:route/GET:/api/items");
        Assert.Equal(2, view.Total);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Owner_pages_come_first_then_suggested_ones_by_action_count_and_an_id_clash_takes_a_suffix()
    {
        var owner = PagesWorld.Named("settings", "Preferences", PagesWorld.Week);

        var view = await Read(PagesWorld.Query(), PagesWorld.Human(owner));

        Assert.Equal([("settings", "owner"), ("settings-2", "suggested")], view.Pages.Select(p => (p.Id.Value, p.Mark)));
        Assert.Equal(PagesWorld.Settings, view.Pages[1].Key.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Marks_keeps_only_the_owner_or_only_the_suggested_pages()
    {
        var owner = PagesWorld.Named("week", "Week", PagesWorld.Week);

        var owned = await Read(PagesWorld.Query(PageMarks.Owner), PagesWorld.Human(owner));
        var suggested = await Read(PagesWorld.Query(PageMarks.Suggested), PagesWorld.Human(owner));

        Assert.Equal(["week"], owned.Pages.Select(p => p.Id.Value), StringComparer.Ordinal);
        Assert.Equal(["settings"], suggested.Pages.Select(p => p.Id.Value), StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("SCHEDUL", PagesWorld.Week)]
    [InlineData("get /schedule", PagesWorld.Week)]
    [InlineData("settingsscreen", PagesWorld.Settings)]
    public async Task Q_keeps_the_pages_whose_name_or_map_label_contains_it_ignoring_case(string q, string key)
    {
        var view = await Read(PagesWorld.Query() with { Q = q }, PagesWorld.Human());

        Assert.Equal((key, 1), (Assert.Single(view.Pages).Key.Value, view.Total));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("nothing like it", 0)]
    [InlineData("   ", 2)]
    public async Task Q_that_matches_none_answers_no_pages_and_a_blank_one_keeps_all(string q, int total)
    {
        var view = await Read(PagesWorld.Query() with { Q = q }, PagesWorld.Human());

        Assert.Equal((total, total), (view.Pages.Count, view.Total));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_concept_filter_keeps_the_pages_that_express_it_or_have_an_action_that_does()
    {
        var viaAction = await Read(PagesWorld.Query(concept: "self-scheduling"), PagesWorld.Human());
        var direct = await Read(PagesWorld.Query(concept: "approval-workflow"), PagesWorld.Human());

        Assert.Equal(["schedule-week"], viaAction.Pages.Select(p => p.Id.Value), StringComparer.Ordinal);
        Assert.Equal(["settings"], direct.Pages.Select(p => p.Id.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unknown_concept_is_258_and_names_the_nearest_one()
    {
        var error = await Refused(PagesWorld.Query(concept: "self-schedulin"));

        Assert.Equal(ContextErrors.PageQueryUnknown.Code, error.Code);
        Assert.Contains("context_pages: no concept 'self-schedulin' (fix: try self-scheduling)", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unknown_page_is_257_and_names_the_nearest_one()
    {
        var error = await Refused(PagesWorld.Query(page: "schedul-week"));

        Assert.Equal(ContextErrors.PageNotFound.Code, error.Code);
        Assert.Contains("no page is named 'schedul-week' (fix: try schedule-week)", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unknown_repo_is_258()
    {
        var error = await Refused(PagesWorld.Query(repo: "wbe"));

        Assert.Equal(ContextErrors.PageQueryUnknown.Code, error.Code);
        Assert.Contains("context_pages: no repo 'wbe'", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Limit_and_offset_slice_the_pages_and_total_counts_them_all()
    {
        var first = await Read(PagesWorld.Query(limit: 1), PagesWorld.Human());
        var second = await Read(PagesWorld.Query(limit: 1, offset: 1), PagesWorld.Human());

        Assert.Equal(["schedule-week"], [.. first.Pages.Select(p => p.Id.Value)], StringComparer.Ordinal);
        Assert.Equal((2, true), (first.Total, first.Truncated));
        Assert.Equal(["settings"], [.. second.Pages.Select(p => p.Id.Value)], StringComparer.Ordinal);
        Assert.Equal((2, false), (second.Total, second.Truncated));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(0)]
    [InlineData(201)]
    public async Task A_limit_outside_1_to_200_is_refused(int limit)
    {
        var error = await Refused(PagesWorld.Query(limit: limit));

        Assert.Equal(ContextErrors.QueryInvalid.Code, error.Code);
        Assert.Contains("'limit' is at ", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_of_another_repo_sees_none_of_the_owner_page_and_the_pages_repo_sees_it()
    {
        _ = await _world.ImportYaml(PagesWorld.Yaml());
        var owner = PagesWorld.Named("schedule-week", "Schedule week", PagesWorld.Week);
        var repos = new[] { PagesWorld.Repo(owner), new ContextRepo(ContextRepoName.From("other"), "/work/other") };
        var own = MapWorld.Session("aw-split-panes") with { Repos = repos };
        var foreign = MapWorld.Session("other") with { Repos = repos };

        var seen = (await _world.Engine.Pages(PagesWorld.Query(), own, MapWorld.None)).Value;
        var refused = await _world.Engine.Pages(PagesWorld.Query(), foreign, MapWorld.None);

        Assert.Contains(seen.Pages, p => string.Equals(p.Id.Value, "schedule-week", StringComparison.Ordinal) && string.Equals(p.Mark, "owner", StringComparison.Ordinal));
        Assert.True(refused.IsError ? refused.Error.Code == ContextErrors.UnknownAnchor.Code : refused.Value.Pages.Count == 0);
    }
}
