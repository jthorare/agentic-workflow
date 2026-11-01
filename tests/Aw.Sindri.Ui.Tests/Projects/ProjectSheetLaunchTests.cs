using Aw.Kernel.Testing;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>A project link the system can't open says so on the sheet, with the link to copy (#1712).</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectSheetLaunchTests
{
    private readonly ProjectSheetFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_refused_project_link_shows_the_failed_line_and_a_later_open_clears_it()
    {
        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);
        _f.Launcher.Refuse = true;

        await sheet.Links[0].Open(Token);

        Assert.True(sheet.LinkLaunch.Failed);
        Assert.Equal("Couldn't open the link: https://linear.app/acme/project/5", sheet.LinkLaunch.FailedText);

        _f.Launcher.Refuse = false;
        await sheet.Links[0].Open(Token);

        Assert.False(sheet.LinkLaunch.Failed);
        Assert.Equal(string.Empty, sheet.LinkLaunch.FailedText);
    }
}
