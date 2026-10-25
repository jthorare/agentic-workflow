using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Views;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>Revealing a setup step keeps the page title in view while the step fits in the first screenful (ux-pass B-8).</summary>
[Lane(TestLane.Unit)]
public sealed class RevealScrollTests
{
    [Fact]
    public void A_step_in_the_first_screenful_leaves_the_page_at_the_top() =>
        Assert.Equal(0, RevealScroll.Offset(rowTop: 260, rowBottom: 420, viewportHeight: 800));

    [Fact]
    public void A_step_below_the_first_screenful_scrolls_to_its_top() =>
        Assert.Equal(900, RevealScroll.Offset(rowTop: 900, rowBottom: 1100, viewportHeight: 800));
}
