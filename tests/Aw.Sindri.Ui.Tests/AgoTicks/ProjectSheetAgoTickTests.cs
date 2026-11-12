using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.AgoTicks;

/// <summary>#3347: the project sheet's "Active 5m ago" moves with the clock on the one-second beat.</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectSheetAgoTickTests
{
    private const string Attached = "prj_00000000000000000000000008";

    [Fact]
    public Task The_sheet_activity_words_move_with_the_clock() => UiThread.Run(async () =>
    {
        var host = new AgoTickHost(AgoTickHost.GoldenDay);
        var one = JsonNode.Parse(GoldenFiles.Api("projects-id", "attached"))!.AsObject();
        one["project"]!["id"] = Attached;
        AgoTickHost.SetEvery(one["project"], "lastActivity", host.Clock.GetUtcNow() - TimeSpan.FromMinutes(5));
        host.Server.ProjectAnswers[Attached] = new RouteAnswer(HttpStatusCode.OK, one.ToJsonString());
        var shell = await host.Start();
        shell.Navigate(new DeepLink(DeepLinkKind.Project, Attached));
        await AgoTickHost.Until(() => shell.ProjectSheet.IsShown && shell.ProjectSheet.ActivityText is not null);
        Assert.Equal("Active 5m ago", shell.ProjectSheet.ActivityText);

        host.Clock.Advance(TimeSpan.FromMinutes(2));

        await AgoTickHost.Until(() => string.Equals(shell.ProjectSheet.ActivityText, "Active 7m ago", StringComparison.Ordinal));
    }, AgoTickHost.Token);
}
