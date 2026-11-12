using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.AgoTicks;

/// <summary>#3347: an ended agent row's "ended 30s ago" moves on the one-second beat, between the 5 s polls.</summary>
[Lane(TestLane.Unit)]
public sealed class EndedSessionAgoTickTests
{
    [Fact]
    public Task The_ended_row_words_move_with_the_clock() => UiThread.Run(async () =>
    {
        var asOf = DateTimeOffset.Parse("2026-10-16T12:05:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var host = new AgoTickHost(asOf);
        var list = JsonNode.Parse(GoldenFiles.Api("sessions", "recently-ended"))!;
        AgoTickHost.SetEvery(list["recentlyEnded"], "endedAt", asOf - TimeSpan.FromSeconds(30));
        host.Server.SessionAnswer = new RouteAnswer(HttpStatusCode.OK, list.ToJsonString());
        var shell = await host.Start();
        shell.GoTo(PageId.Agents);
        await AgoTickHost.Until(() => shell.Agents.Ended.Count > 0);
        var row = shell.Agents.Ended[0];
        var before = row.EndedText;

        host.Clock.Advance(TimeSpan.FromSeconds(2));   // under the 5 s poll: only the one-second beat can re-word it

        await AgoTickHost.Until(() => !string.Equals(row.EndedText, before, StringComparison.Ordinal));
    }, AgoTickHost.Token);
}
