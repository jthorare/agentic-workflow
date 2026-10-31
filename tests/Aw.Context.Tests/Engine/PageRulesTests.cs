using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;

namespace Aw.Context.Tests.Engine;

public sealed class PageRulesTests
{
    private static readonly PageFacts _facts = new(
        new HashSet<ConceptId> { ConceptId.From("self-scheduling"), ConceptId.From("approval-workflow") },
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { ["surface:route/GET:/schedule/week"] = ["web"], ["surface:screen/Shared"] = ["web", "admin"] },
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { ["action:SchedulePage.submit-schedule"] = ["web"] });

    private static PagePin Page(string id = "schedule-week", string name = "Schedule week", string? repo = null, string? surface = null, params PageActionPin[] actions) =>
        new(PageId.From(id), name, repo is null ? null : ContextRepoName.From(repo), surface, actions);

    private static PageActionPin Action(string id = "submit", string name = "Submit a schedule", string? key = null, params string[] concepts) => new(PageActionId.From(id), name, key, concepts);

    private static string[] Codes(params PagePin[] pages) => [.. PageRules.Check(pages, _facts).Select(e => e.Code)];

    [Fact, Trait("Lane", "unit")]
    public void A_page_action_naming_an_unknown_concept_is_refused_at_write()
    {
        var codes = Codes(Page(actions: Action(concepts: "aproval")));
        Assert.Equal([ContextErrors.PageConceptUnknown.Code], codes);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_page_naming_its_map_action_and_surface_with_capitals_passes_the_write_check()
    {
        var facts = new PageFacts(
            new HashSet<ConceptId>(),
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { ["surface:screen/SettingsScreen"] = ["web"] },
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { ["action:weekpage.submit"] = ["web"] })
        { Stored = EntryKey.Normalized };

        var errors = PageRules.Check([Page(surface: "surface:screen/SettingsScreen", actions: Action(key: "action:WeekPage.submit"))], facts);

        Assert.Empty(errors);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pin_id_and_a_derived_concept_key_both_name_a_concept()
        => Assert.Empty(Codes(Page(actions: Action(concepts: ["self-scheduling", "concept:approval-workflow"]))));

    [Fact, Trait("Lane", "unit")]
    public void A_surface_or_action_key_the_map_lacks_is_refused()
        => Assert.Equal([ContextErrors.PageKeyUnknown.Code, ContextErrors.PageKeyUnknown.Code], Codes(Page(surface: "surface:route/GET:/schedule/wk", actions: Action(key: "action:Nope.x"))));

    [Fact, Trait("Lane", "unit")]
    public void A_surface_key_in_two_repos_needs_a_repo()
    {
        Assert.Equal([ContextErrors.PageRepoAmbiguous.Code], Codes(Page(surface: "surface:screen/Shared")));
        Assert.Empty(Codes(Page(surface: "surface:screen/Shared", repo: "admin")));
    }

    [Fact, Trait("Lane", "unit")]
    public void Duplicate_page_ids_and_duplicate_action_ids_within_a_page_are_refused()
    {
        var dupPages = Codes(Page(), Page());
        var dupActions = Codes(Page(actions: [Action(), Action()]));
        Assert.Equal([ContextErrors.PageIdDuplicate.Code], dupPages);
        Assert.Equal([ContextErrors.PageIdDuplicate.Code], dupActions);
    }

    [Fact, Trait("Lane", "unit")]
    public void Names_are_one_to_eighty_characters_after_control_characters_and_without_edge_spaces()
    {
        Assert.Equal([ContextErrors.PageNameInvalid.Code], Codes(Page(name: new string('a', 81))));
        Assert.Equal([ContextErrors.PageNameInvalid.Code], Codes(Page(name: "\u0007")));
        Assert.Equal([ContextErrors.PageNameInvalid.Code], Codes(Page(name: " Schedule")));
        Assert.Empty(Codes(Page(name: "Sche\u0007dule")));
    }

    private static void AssertOverCap(string[] codes) => Assert.Contains(ContextErrors.PageOverCap.Code, codes, StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void More_than_the_caps_is_refused()
    {
        var manyActions = Enumerable.Range(0, 41).Select(i => Action(id: string.Create(CultureInfo.InvariantCulture, $"a{i}"), name: string.Create(CultureInfo.InvariantCulture, $"A{i}"))).ToArray();
        var manyConcepts = Enumerable.Range(0, 21).Select(i => string.Create(CultureInfo.InvariantCulture, $"c{i}")).ToArray();
        var manyPages = Enumerable.Range(0, 201).Select(i => Page(id: string.Create(CultureInfo.InvariantCulture, $"p{i}"), name: string.Create(CultureInfo.InvariantCulture, $"P{i}"))).ToArray();
        AssertOverCap(Codes(Page(actions: manyActions)));
        AssertOverCap(Codes(Page(actions: Action(concepts: manyConcepts))));
        AssertOverCap(Codes(manyPages));
    }
}
