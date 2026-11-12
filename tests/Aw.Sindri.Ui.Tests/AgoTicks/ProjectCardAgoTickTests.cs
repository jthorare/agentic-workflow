using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.AgoTicks;

/// <summary>#3347: a project card's "5m ago" moves with the clock on the one-second beat.</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectCardAgoTickTests
{
    [Fact]
    public Task The_card_footer_words_move_with_the_clock() => UiThread.Run(async () =>
    {
        var host = new AgoTickHost(AgoTickHost.GoldenDay);
        var list = JsonNode.Parse(GoldenFiles.Api("projects", "mixed"))!;
        AgoTickHost.SetEvery(list["projects"], "lastActivity", host.Clock.GetUtcNow() - TimeSpan.FromMinutes(5));
        host.Server.ProjectsAnswer = new RouteAnswer(HttpStatusCode.OK, list.ToJsonString());
        var shell = await host.Start();
        shell.GoTo(PageId.Projects);
        await AgoTickHost.Until(() => shell.Projects.Groups.Any(g => g.Cards.Count > 0));
        var card = shell.Projects.Groups.First(g => g.Cards.Count > 0).Cards[0];
        var before = card.FooterText;
        Assert.Contains("5m ago", before, StringComparison.Ordinal);

        host.Clock.Advance(TimeSpan.FromMinutes(2));

        await AgoTickHost.Until(() => card.FooterText.Contains("7m ago", StringComparison.Ordinal));
    }, AgoTickHost.Token);
}
