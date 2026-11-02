using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>#1724 P2b: a concept's Why drill lists its pages and actions after its decisions and records (pages spec §3.4, §3.5).</summary>
[Lane(TestLane.Unit)]
public sealed class PagesSectionTests
{
    // Self scheduling: a decision, an owner-named page with one action, and two suggested pages with two and one actions, in the server's section order.
    internal static WholeMapView Scheduling()
    {
        var map = WholeMapLayoutTests.Draft("nodes-small");
        var area = map.Areas[0].Key;
        WholeNode Node(string key, string label, bool owner = false) =>
            new(key, label, key[..key.IndexOf(':', StringComparison.Ordinal)], area, [], "trusted", owner ? "human" : "deterministic", 1, Confirmed: owner) { Owner = owner };
        WholeEdge Edge(string from, string type, string to) => new(from, to, type, $"edge:{from}>{to}", "deterministic", 1, Confirmed: false);
        return map with
        {
            Areas = [map.Areas[0]],
            AreaEdges = [],
            Nodes =
            [
                Node("decision:approve-first", "Approve before publishing"),
                Node("concept:approval", "Approval workflow"),
                Node("surface:screen/time-off", "Time-off request", owner: true), Node("action:send", "Send a request", owner: true),
                Node("surface:screen/week", "Schedule week"), Node("action:submit", "Submit schedule"), Node("action:copy", "Copy last week"),
                Node("surface:screen/availability", "Availability"), Node("action:save", "Save availability"),
                Node("code:web/src/SubmitButton.tsx", "SubmitButton.tsx"),
            ],
            Edges =
            [
                Edge("decision:approve-first", "decides", "action:submit"),
                Edge("action:send", "on", "surface:screen/time-off"),
                Edge("action:submit", "on", "surface:screen/week"), Edge("action:copy", "on", "surface:screen/week"),
                Edge("action:submit", "expresses", "concept:approval"),
                Edge("action:save", "on", "surface:screen/availability"),
                Edge("code:web/src/SubmitButton.tsx", "implements", "action:submit"),
            ],
            Pages = ["surface:screen/time-off", "surface:screen/week", "surface:screen/availability"],
        };
    }

    internal static WholeMapViewModel WhyDrill(WholeMapView answer)
    {
        var map = new WholeMapViewModel(() => TimeZoneInfo.Utc);
        map.EnterConcept("Self scheduling");
        map.Show(answer);
        map.DrillInto(WholeBand.Intent);
        return map;
    }

    [Fact]
    public void Concept_why_drill_lists_its_owner_page_first_then_suggested()
    {
        var map = WhyDrill(Scheduling());

        Expect.True(map.ShowPages, "the section shows on the Why drill");
        Expect.Sequence(["Time-off request", "Schedule week", "Availability"], map.Pages.Rows.Select(r => r.Name));
        Expect.Sequence(["Set by a person", "No code found yet"], map.Pages.Rows[0].Marks);
        Expect.Sequence(["Suggested", "Code found"], map.Pages.Rows[1].Marks);
    }

    [Fact]
    public void An_action_row_names_its_page_its_other_concepts_its_decisions_and_its_code()
    {
        var week = WhyDrill(Scheduling()).Pages.Rows[1];

        Expect.Equal("From the map: week", week.FromMap);
        Expect.Sequence(["Copy last week", "Submit schedule"], week.Actions.Select(a => a.Name));
        Expect.Equal("· on Schedule week · also in Approval workflow · 1 decision · code: SubmitButton.tsx", week.Actions[1].Detail);
        Expect.Equal("· on Schedule week · No code found yet", week.Actions[0].Detail);
    }

    [Fact]
    public void The_opened_concept_is_not_an_also_in()
    {
        var map = new WholeMapViewModel(() => TimeZoneInfo.Utc);
        map.EnterConcept("Approval workflow", "concept:approval");
        map.Show(Scheduling());
        map.DrillInto(WholeBand.Intent);

        Expect.Equal("· on Schedule week · 1 decision · code: SubmitButton.tsx", map.Pages.Rows[1].Actions[1].Detail);
    }

    [Fact]
    public void A_route_reads_as_its_method_and_path()
    {
        var answer = Scheduling() with { Nodes = [.. Scheduling().Nodes!, Scheduling().Nodes![4] with { Key = "surface:route/GET:/schedule/week" }], Pages = ["surface:route/GET:/schedule/week"] };

        Expect.Equal("From the map: GET /schedule/week", WhyDrill(answer).Pages.Rows[0].FromMap);
    }

    [Fact]
    public void Past_twenty_pages_show_all_lists_the_rest()
    {
        var many = Scheduling();
        var pages = Enumerable.Range(1, 34).Select(i => many.Nodes![4] with { Key = "surface:screen/p" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), Label = "Page " + i.ToString(System.Globalization.CultureInfo.InvariantCulture) }).ToList();
        var map = WhyDrill(many with { Nodes = [.. many.Nodes!, .. pages], Pages = [.. pages.Select(p => p.Key)] });

        Expect.Equal((20, true, "Show all 34"), (map.Pages.Rows.Count, map.Pages.HasMore, map.Pages.ShowAllText));
        map.Pages.ShowAll();
        Expect.Equal((34, false), (map.Pages.Rows.Count, map.Pages.HasMore));
    }

    [Fact]
    public void A_concept_with_no_pages_says_how_to_name_one()
    {
        var map = WhyDrill(Scheduling() with { Pages = [] });

        Expect.Equal((true, "No pages or actions found for this concept yet. Name one with New page."), (map.Pages.IsEmpty, map.Pages.StatusText));
    }

    [Fact]
    public void A_failed_read_says_so_with_try_again()
    {
        var map = WhyDrill(Scheduling());
        map.Loading();
        Expect.Equal("Loading pages…", map.Pages.StatusText);

        map.Failed();
        Expect.Equal((true, "Couldn't read this concept's pages just now. Try again.", "Try again"), (map.Pages.IsFailed, map.Pages.StatusText, map.Pages.RetryText));
    }

    [Fact]
    public void The_other_bands_and_the_concept_layer_do_not_show_the_section()
    {
        var map = new WholeMapViewModel(() => TimeZoneInfo.Utc);
        map.EnterConcept("Self scheduling");
        map.Show(Scheduling());
        Expect.True(!map.ShowPages, "not before a drill");

        map.DrillInto(WholeBand.Product);
        Expect.True(!map.ShowPages, "not on the Product band");

        map.LeaveConcept();
        Expect.Equal((false, 0), (map.ShowPages, map.Pages.Rows.Count));
    }
}
