using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.AgoTicks;

/// <summary>#3347: a review row's "10 min" age moves with the clock on the one-second beat.</summary>
[Lane(TestLane.Unit)]
public sealed class ReviewRowAgoTickTests
{
    [Fact]
    public Task The_row_age_moves_with_the_clock() => UiThread.Run(async () =>
    {
        var host = new AgoTickHost(AgoTickHost.GoldenDay);
        var list = JsonNode.Parse(GoldenFiles.Api("reviews", "some"))!;
        AgoTickHost.SetEvery(list, "openedAt", host.Clock.GetUtcNow() - TimeSpan.FromMinutes(10));
        host.Server.ReviewsAnswer = new RouteAnswer(HttpStatusCode.OK, list.ToJsonString());
        var shell = await host.Start();
        shell.GoTo(PageId.Reviews);
        await AgoTickHost.Until(() => shell.Reviews.Groups.SelectMany(g => g.Sections).SelectMany(s => s.Rows).Any());
        var row = shell.Reviews.Groups.SelectMany(g => g.Sections).SelectMany(s => s.Rows).First();
        Assert.Equal("10 min", row.Age);

        host.Clock.Advance(TimeSpan.FromMinutes(2));

        await AgoTickHost.Until(() => string.Equals(row.Age, "12 min", StringComparison.Ordinal));
    }, AgoTickHost.Token);
}
