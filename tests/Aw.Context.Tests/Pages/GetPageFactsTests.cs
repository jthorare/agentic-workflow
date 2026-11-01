using Aw.Context.Core.Engine;
using Aw.Context.Core.Pages;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.Pages;

/// <summary>The map read the page write checks against (pages spec §3.1): concepts, and the repos of each surface and action key.</summary>
public sealed class GetPageFactsTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private async Task<PageMapFacts> Facts(ContextScope scope, string? yaml = null, bool import = true)
    {
        if (import)
        {
            _ = await _world.ImportYaml(yaml ?? PagesWorld.Yaml());
        }

        return (await _world.Engine.PageFacts(scope, MapWorld.None)).Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Page_facts_list_concepts_surfaces_and_actions_with_their_repos()
    {
        var facts = await Facts(PagesWorld.Human());

        Assert.StartsWith("v", facts.MapVersion, StringComparison.Ordinal);
        Assert.Equal(["approval-workflow", "billing", "self-scheduling"], facts.Facts.ConceptIds.Select(c => c.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(["Billing", "Self-scheduling"], facts.Concepts.Where(c => new[] { "billing", "self-scheduling" }.Contains(c.Id.Value, StringComparer.Ordinal)).Select(c => c.Name).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(["aw-split-panes"], facts.Facts.SurfaceRepos[PagesWorld.Week], StringComparer.Ordinal);
        Assert.Equal(["aw-split-panes"], facts.Facts.ActionRepos["action:weekpage.submit"], StringComparer.Ordinal);
        Assert.Empty(facts.Facts.ActionRepos["action:weekpage.cancel"]);
        Assert.NotNull(facts.Facts.Stored);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Page_facts_without_a_map_are_empty()
    {
        var facts = await Facts(PagesWorld.Human(), import: false);

        Assert.Null(facts.MapVersion);
        Assert.Empty(facts.Facts.ConceptIds);
        Assert.Empty(facts.Facts.SurfaceRepos);
        Assert.Empty(facts.Facts.ActionRepos);
        Assert.Empty(facts.Concepts);
        Assert.NotNull(facts.Facts.Stored);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_surface_a_deny_rule_hides_is_not_in_the_page_facts()
    {
        var repo = new ContextRepo(ContextRepoName.From("aw-split-panes"), "/work/aw-split-panes") { DenyPaths = ["src/Week.tsx"] };

        var facts = await Facts(MapWorld.HumanWith(MapWorld.OnlyFixtureRepo, [repo]));

        Assert.False(facts.Facts.SurfaceRepos.ContainsKey(PagesWorld.Week));
        Assert.True(facts.Facts.SurfaceRepos.ContainsKey(PagesWorld.Settings));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Page_facts_find_a_capitalised_action_key_through_its_stored_form()
    {
        var facts = await Facts(PagesWorld.Human(), PagesWorld.Yaml().Replace("action:WeekPage.", "action:weekpage.", StringComparison.Ordinal));

        Assert.False(facts.Facts.ActionRepos.ContainsKey("action:WeekPage.submit"));
        Assert.True(facts.Facts.ActionRepos.ContainsKey(facts.Facts.Stored!("action:WeekPage.submit")));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_surface_evidenced_in_two_repos_lists_both()
    {
        var yaml = PagesWorld.Yaml().Replace("repo:aw-split-panes/src/Settings.tsx\"}]}", "repo:aw-split-panes/src/Settings.tsx\"}, {key: \"repo:aw-other/src/Settings.tsx\"}]}", StringComparison.Ordinal);

        var facts = await Facts(PagesWorld.Human(), yaml);

        Assert.Equal(["aw-other", "aw-split-panes"], facts.Facts.SurfaceRepos[PagesWorld.Settings], StringComparer.Ordinal);
    }
}
