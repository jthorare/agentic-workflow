using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>A pull request link the system can't open from the inbox says so, with the link to copy (#1722).</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouPrLaunchTests
{
    private static readonly Uri _pr = new("https://github.com/acme/web/pull/12");

    private static async Task<NeedsYouPageViewModel> Page(FakeLauncher launcher)
    {
        var clock = new FakeTimeProvider(A.Noon);
        var list = new NeedsYouViewModel(new Preferences(new InMemoryPreferencesStore()), new FakeMotionPreference(), clock);
        await list.Restore(TestContext.Current.CancellationToken);
        list.Update(new NeedsYouFeed([NeedsYouRows.Decision()], string.Empty));
        return new NeedsYouPageViewModel(list, clock, new FakeClipboard(), new DeskReporter(new FakeSindriApi(), () => new Uri("http://127.0.0.1:7410/"), launcher));
    }

    [Fact]
    public async Task A_refused_pull_request_link_shows_the_failed_line_with_the_link()
    {
        var page = await Page(new FakeLauncher { Refuse = true });

        await page.OpenPrLink(_pr);

        Assert.True(page.PrLaunch.Failed);
        Assert.Equal(Labels.LaunchFailed(_pr.AbsoluteUri), page.PrLaunch.FailedText);
    }

    [Fact]
    public async Task A_pull_request_link_that_opens_shows_no_failed_line()
    {
        var launcher = new FakeLauncher();
        var page = await Page(launcher);

        await page.OpenPrLink(_pr);

        Assert.False(page.PrLaunch.Failed);
        Assert.Equal([_pr.AbsoluteUri], launcher.Opened);
    }
}
