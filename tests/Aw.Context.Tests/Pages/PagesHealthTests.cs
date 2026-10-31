using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.Pages;

/// <summary>Doctor 'context:pages' (pages spec §3.7): the approved profile's pages against the stored map, without the overlay.</summary>
public sealed class PagesHealthTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static PageActionPin Action(string key, params string[] concepts) => new(PageActionId.From("submit"), "Submit", key, concepts);

    private async Task<HealthRow> Check(ContextScope? scope, bool import = true)
    {
        if (import)
        {
            _ = await _world.ImportYaml(PagesWorld.Yaml());
        }

        var health = (await _world.Engine.CheckPages(scope, MapWorld.None)).Value;
        return new HealthRow(health.Name, health.State, health.Detail, health.Fix);
    }

    private sealed record HealthRow(string Name, MapHealthState State, string Detail, string? Fix);

    [Fact, Trait("Lane", "unit")]
    public async Task No_scope_or_no_pages_is_ok_and_says_no_pages_named()
    {
        var none = await Check(scope: null);
        var empty = await Check(PagesWorld.Human(), import: false);

        Assert.Equal(("context:pages", MapHealthState.Ok, "no pages named"), (none.Name, none.State, none.Detail));
        Assert.Equal((MapHealthState.Ok, "no pages named"), (empty.State, empty.Detail));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Pages_with_no_map_are_ok_and_say_no_map_yet()
    {
        var health = await Check(PagesWorld.Human(PagesWorld.Named("week", "Week", PagesWorld.Week)), import: false);

        Assert.Equal((MapHealthState.Ok, "no map yet"), (health.State, health.Detail));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Pages_whose_keys_and_concepts_are_on_the_map_are_ok()
    {
        var page = PagesWorld.Named("week", "Week", PagesWorld.Week, Action("action:WeekPage.submit", "self-scheduling"));

        var health = await Check(PagesWorld.Human(page));

        Assert.Equal((MapHealthState.Ok, "1 page, all on the map"), (health.State, health.Detail));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_surface_key_the_map_no_longer_has_is_a_warning_that_says_gone()
    {
        var health = await Check(PagesWorld.Human(PagesWorld.Named("week", "Week", "surface:route/GET:/gone")));

        Assert.Equal(MapHealthState.Warn, health.State);
        Assert.Contains("page 'week': surface surface:route/GET:/gone is gone from the map", health.Detail, StringComparison.Ordinal);
        Assert.Equal("sindri context page list --suggested", health.Fix);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_concept_the_map_no_longer_has_is_a_warning()
    {
        var health = await Check(PagesWorld.Human(PagesWorld.Named("week", "Week", PagesWorld.Week, Action("action:WeekPage.submit", "nope"))));

        Assert.Equal(MapHealthState.Warn, health.State);
        Assert.Contains("concept concept:nope is gone from the map", health.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Several_problems_give_the_count_and_the_first_one()
    {
        var health = await Check(PagesWorld.Human(PagesWorld.Named("week", "Week", "surface:route/GET:/gone", Action("action:Gone.x", "nope"))));

        Assert.StartsWith("3 problems; first: page 'week': surface", health.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_key_hidden_by_a_deny_glob_is_a_warning_that_says_hidden_not_gone()
    {
        var repo = new ContextRepo(ContextRepoName.From("aw-split-panes"), "/work/aw-split-panes") { DenyPaths = ["src/Week.tsx"], Pages = [PagesWorld.Named("week", "Week", PagesWorld.Week)] };

        var health = await Check(MapWorld.HumanWith(MapWorld.OnlyFixtureRepo, [repo]));

        Assert.Equal(MapHealthState.Warn, health.State);
        Assert.Contains("is hidden by a deny rule", health.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_page_over_the_action_cap_is_a_warning()
    {
        var actions = Enumerable.Range(0, 41).Select(i => new PageActionPin(PageActionId.From("a" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)), "A", ActionKey: null, Concepts: [])).ToArray();

        var health = await Check(PagesWorld.Human(PagesWorld.Named("week", "Week", PagesWorld.Week, actions)));

        Assert.Equal(MapHealthState.Warn, health.State);
        Assert.Contains("41 actions; at most 40", health.Detail, StringComparison.Ordinal);
    }
}
