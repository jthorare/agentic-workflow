using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>An overlay gives focus back to the control that opened it, not to the page region that held focus (ux-pass C-10).</summary>
[Lane(TestLane.Unit)]
public sealed class OverlayOpenerTests
{
    private const string Page = "page";

    [Fact]
    public void Focus_on_the_page_region_gives_way_to_the_control_that_invoked_the_overlay() =>
        Assert.Equal("start", OverlayOpener.Choose(Page, Page, "start"));

    [Fact]
    public void Focus_on_nothing_gives_way_to_the_invoker() =>
        Assert.Equal("start", OverlayOpener.Choose(focused: null, Page, "start"));

    [Fact]
    public void A_focused_control_stays_the_opener_even_with_an_invoker() =>
        Assert.Equal("field", OverlayOpener.Choose("field", Page, "start"));

    [Fact]
    public void A_second_opener_such_as_Create_item_is_chosen_the_same_way() =>
        Assert.Equal("create-item", OverlayOpener.Choose(Page, Page, "create-item"));

    [Fact]
    public void Without_an_invoker_the_focused_element_is_kept() =>
        Assert.Equal(Page, OverlayOpener.Choose(Page, Page, invoker: null));
}
