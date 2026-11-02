using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>The drill's state on the whole map inside a concept (#1289): the buttons, the two modes, the crumbs and the words, and how Back leaves it.</summary>
[Lane(TestLane.Unit)]
public sealed class WholeMapBandDrillViewModelTests
{
    private static WholeMapViewModel Concept()
    {
        var map = new WholeMapViewModel(() => TimeZoneInfo.Utc);
        map.EnterConcept("Self scheduling");
        map.Show(WholeMapLayoutTests.Draft("nodes-small"));
        return map;
    }

    private static WholeMapViewModel Drilled(WholeBand band)
    {
        var map = Concept();
        map.DrillInto(band);
        return map;
    }

    [Fact]
    public void Inside_a_concept_there_is_a_button_per_drawn_band_top_to_bottom()
    {
        var map = Concept();

        Expect.Sequence(["Why", "Product", "Code"], map.BandButtons.Select(b => b.Title));
        Expect.Sequence(["Open the Why band", "Open the Product band", "Open the Code band"], map.BandButtons.Select(b => b.Name));
        Expect.True(map.ShowBandButtons);
        Expect.True(!map.IsDrilled);
    }

    [Fact]
    public void Outside_a_concept_there_are_no_band_buttons()
    {
        var map = new WholeMapViewModel(() => TimeZoneInfo.Utc);
        map.Show(WholeMapLayoutTests.Draft("nodes-small"));

        Expect.True(!map.ShowBandButtons);
    }

    [Fact]
    public void Drilling_in_opens_across_and_says_so_in_the_two_segments()
    {
        var map = Drilled(WholeBand.Intent);

        Expect.True(map.IsDrilled);
        Expect.True(!map.ShowBandButtons);
        Expect.Equal(("Links to other bands", "Links inside Why", true, false), (map.CrossText, map.OwnText, map.ShowCross, map.ShowOwn));
    }

    [Fact]
    public void The_own_segment_draws_only_the_bands_own_nodes()
    {
        var map = Drilled(WholeBand.Product);
        var across = map.Shown!.Nodes.Count;

        map.ShowOwn = true;

        Expect.Equal((false, true), (map.ShowCross, map.ShowOwn));
        Expect.True(map.Shown!.Nodes.All(n => NodeBands.Of(n.Kind) == WholeBand.Product));
        Expect.True(map.Shown.Nodes.Count != across || map.Shown.Edges.All(e => map.Shown.Nodes.Any(n => n.Key == e.From) && map.Shown.Nodes.Any(n => n.Key == e.To)));
    }

    [Fact]
    public void The_canvas_name_lists_only_the_bands_still_drawn_with_what_is_drawn()
    {
        var map = Drilled(WholeBand.Intent);
        var bands = map.Shown!.Bands;

        Expect.Equal(Labels.ContextWholeCanvasBands(bands.OrderBy(b => b.Top).Select(b => (NodeBands.Title(b.Band), b.Meta))), map.CanvasName);
        Expect.True(map.CanvasName.Contains("Why (", StringComparison.Ordinal));
    }

    [Fact]
    public void An_empty_mode_says_so()
    {
        var map = Drilled(WholeBand.Intent);
        map.ShowOwn = true;
        map.ShowOwn = true;

        Expect.Equal(map.Shown!.Edges.Count == 0, map.ShowDrillEmpty);
        Expect.Equal(map.ShowDrillEmpty ? "No links inside Why." : string.Empty, map.DrillEmptyText);
        map.ShowCross = true;
        Expect.Equal(map.Shown!.Edges.Count == 0 ? "No links from Why to the other bands." : string.Empty, map.DrillEmptyText);
    }

    [Fact]
    public void The_crumbs_run_from_the_concepts_through_the_concept_to_the_band()
    {
        var map = Drilled(WholeBand.Intent);

        Expect.Sequence(["Concepts", "Self scheduling", "Why"], map.Crumbs.Select(c => c.Text));
        Expect.Sequence([true, true, false], map.Crumbs.Select(c => c.IsLink));
    }

    [Fact]
    public void Back_leaves_the_drill_and_a_second_back_goes_up_as_before()
    {
        var map = Drilled(WholeBand.Code);
        var up = 0;
        map.UpRequested += (_, _) => up++;

        map.GoUp();
        Expect.Equal((false, 0, true), (map.IsDrilled, up, map.ShowBandButtons));
        map.GoUp();
        Expect.Equal(1, up);
    }

    [Fact]
    public void The_concept_crumb_leaves_the_drill_like_back()
    {
        var map = Drilled(WholeBand.Code);
        var up = 0;
        map.UpRequested += (_, _) => up++;

        map.Navigate(map.Crumbs[1]);

        Expect.Equal((false, 0), (map.IsDrilled, up));
    }

    [Fact]
    public void The_concepts_crumb_still_goes_to_the_cards()
    {
        var map = Drilled(WholeBand.Code);
        var cards = 0;
        map.ConceptsRequested += (_, _) => cards++;

        map.Navigate(map.Crumbs[0]);

        Expect.Equal(1, cards);
    }

    [Fact]
    public void Entering_or_leaving_a_concept_forgets_the_drill()
    {
        var map = Drilled(WholeBand.Product);
        map.EnterConcept("Billing");
        Expect.True(!map.IsDrilled);

        map.DrillInto(WholeBand.Product);
        map.LeaveConcept();
        Expect.True(!map.IsDrilled);
    }

    [Fact]
    public void A_click_on_a_lane_title_drills_into_that_band()
    {
        var map = Concept();
        var product = map.Geometry!.Bands.Single(b => b.Band == WholeBand.Product);

        var hit = map.LaneTitleClicked(x: WholeMapLayout.Margin, y: product.LaneTop + 10);

        Expect.True(hit);
        Expect.True(map.IsDrilled);
        Expect.Equal("Links inside Product", map.OwnText);
    }
}
