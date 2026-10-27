using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>#1083 (C-16): a lone "~" over the size line reads as a stray mark, so the crumb row shows only when there is a path to walk back along.</summary>
[Lane(TestLane.Unit)]
public sealed class WholeMapRootCrumbTests
{
    private static readonly TimeZoneInfo _utc = TimeZoneInfo.Utc;

    [Fact]
    public void The_whole_map_shows_no_crumb_row_when_only_the_root_is_in_it()
    {
        var map = new WholeMapViewModel(() => _utc);
        map.Show(WholeMapLayoutTests.Draft("nodes-small"));

        Assert.Equal(["~"], map.Crumbs.Select(c => c.Text), StringComparer.Ordinal);
        Assert.False(map.ShowCrumbs);
    }

    [Fact]
    public void An_open_feature_shows_the_crumb_row_with_the_way_back_to_the_root()
    {
        var map = new WholeMapViewModel(() => _utc);
        map.Show(WholeMapLayoutTests.Draft("areas-over-budget"));

        map.ShowArea(WholeMapLayoutTests.Draft("area-open"));

        Assert.Equal(["~", "Tab groups"], map.Crumbs.Select(c => c.Text), StringComparer.Ordinal);
        Assert.True(map.ShowCrumbs);
    }

    [Fact]
    public void A_concept_shows_the_crumb_row_even_before_anything_is_open_in_it()
    {
        var map = new WholeMapViewModel(() => _utc);
        map.Show(WholeMapLayoutTests.Draft("nodes-small"));

        map.EnterConcept("Self scheduling");

        Assert.True(map.ShowCrumbs);
    }

    [Fact]
    public void The_crumb_row_comes_back_and_goes_again_with_the_map_it_belongs_to()
    {
        var map = new WholeMapViewModel(() => _utc);
        var seen = new List<string?>();
        map.PropertyChanged += (_, e) => seen.Add(e.PropertyName);
        map.Show(WholeMapLayoutTests.Draft("areas-over-budget"));
        map.ShowArea(WholeMapLayoutTests.Draft("area-open"));
        seen.Clear();

        map.CloseArea();

        Assert.Contains(nameof(WholeMapViewModel.ShowCrumbs), seen, StringComparer.Ordinal);
        Assert.False(map.ShowCrumbs);
    }
}
