using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Instance;

namespace Aw.Sindri.Ui.Tests.Instance;

/// <summary>A subject link the system refuses to open says so, with the link to copy (#1712, #1682).</summary>
[Lane(TestLane.Unit)]
public sealed class SubjectLineLaunchTests
{
    [Fact]
    public async Task A_refused_OpenIn_shows_the_link_and_a_later_one_that_works_clears_it()
    {
        var launcher = new FakeLauncher { Refuse = true };
        var detail = FixtureInstances.Detail("project-build");
        var line = new SubjectLineViewModel(detail.Subject, detail.Address, launcher);

        await line.OpenIn(TestContext.Current.CancellationToken);

        Assert.True(line.LaunchFailed);
        Assert.Contains("Couldn't open the link", line.LaunchFailedText, StringComparison.Ordinal);
        Assert.Contains(new Uri(line.Href).AbsoluteUri, line.LaunchFailedText, StringComparison.Ordinal);

        launcher.Refuse = false;
        await line.OpenIn(TestContext.Current.CancellationToken);
        Assert.False(line.LaunchFailed);
    }
}
