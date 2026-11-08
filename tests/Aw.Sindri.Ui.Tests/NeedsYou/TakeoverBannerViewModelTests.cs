using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Takeover;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>The takeover banner of a sandboxed run (sandbox spec §7, accessibility row 'Takeover banner'): golden text, read once, dismissible once, back on the next takeover.</summary>
[Lane(TestLane.Unit)]
public sealed class TakeoverBannerViewModelTests
{
    private static string Golden => GoldenFiles.Sandbox("takeover-banner.txt").Trim();

    [Fact]
    public void The_banner_says_the_golden_text_while_a_sandboxed_run_is_taken_over()
    {
        var banner = new TakeoverBannerViewModel();

        banner.Update(sandboxed: true, takenOver: true);

        Assert.Equal((Golden, true), (banner.Text, banner.IsVisible));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void It_does_not_show_for_a_run_that_is_not_sandboxed_or_not_taken_over(bool sandboxed, bool takenOver)
    {
        var banner = new TakeoverBannerViewModel();

        banner.Update(sandboxed, takenOver);

        Assert.False(banner.IsVisible);
    }

    [Fact]
    public void It_is_read_once_on_appearance_and_a_repeat_update_does_not_read_it_again()
    {
        var banner = new TakeoverBannerViewModel();
        banner.Update(sandboxed: true, takenOver: true);
        var changes = 0;
        banner.Voice.PropertyChanged += (_, _) => changes++;

        banner.Update(sandboxed: true, takenOver: true);

        Assert.Equal((Golden, true, 0), (banner.Voice.Announcement, banner.Voice.IsAnnouncing, changes));
    }

    [Fact]
    public void Dismiss_hides_it_for_good_until_the_run_is_handed_back_and_taken_over_again()
    {
        var banner = new TakeoverBannerViewModel();
        banner.Update(sandboxed: true, takenOver: true);

        banner.Dismiss();
        banner.Update(sandboxed: true, takenOver: true);
        Assert.False(banner.IsVisible);

        banner.Update(sandboxed: true, takenOver: false);
        banner.Update(sandboxed: true, takenOver: true);
        Assert.True(banner.IsVisible);
    }
}
