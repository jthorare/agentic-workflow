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
}
