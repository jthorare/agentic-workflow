using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Facts;

namespace Aw.Sindri.Ui.Tests.Facts;

/// <summary>A link fact the system refuses to open says so, with the link to copy (#1712, #1682).</summary>
[Lane(TestLane.Unit)]
public sealed class LinkFactLaunchTests
{
    [Fact]
    public async Task A_refused_Open_shows_the_link_and_a_later_one_that_works_clears_it()
    {
        var launcher = new FakeLauncher { Refuse = true };
        var viewer = new LinkFactViewer(FactFixtures.Link(), launcher);

        await viewer.Open(TestContext.Current.CancellationToken);

        Assert.True(viewer.LaunchFailed);
        Assert.Contains("Couldn't open the link", viewer.LaunchFailedText, StringComparison.Ordinal);
        Assert.Contains(new Uri(viewer.Url).AbsoluteUri, viewer.LaunchFailedText, StringComparison.Ordinal);

        launcher.Refuse = false;
        await viewer.Open(TestContext.Current.CancellationToken);
        Assert.False(viewer.LaunchFailed);
    }
}
