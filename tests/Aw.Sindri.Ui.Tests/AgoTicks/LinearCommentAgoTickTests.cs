using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.AgoTicks;

/// <summary>#3347: a Linear comment's "30m ago" moves with the clock on the one-second beat.</summary>
[Lane(TestLane.Unit)]
public sealed class LinearCommentAgoTickTests
{
    [Fact]
    public Task The_comment_words_move_with_the_clock() => UiThread.Run(async () =>
    {
        var host = new AgoTickHost(AgoTickHost.GoldenDay);
        var key = (string)JsonNode.Parse(GoldenFiles.Api("linear-issue", "full"))!["key"]!;
        var shell = await host.Start();
        shell.Navigate(new DeepLink(DeepLinkKind.Issue, key));
        await AgoTickHost.Until(() => shell.IssueSheet.Comments.Count > 0);
        var before = shell.IssueSheet.Comments[0].When;

        host.Clock.Advance(TimeSpan.FromMinutes(2));

        await AgoTickHost.Until(() => shell.IssueSheet.Comments.Count > 0 && !string.Equals(shell.IssueSheet.Comments[0].When, before, StringComparison.Ordinal));
    }, AgoTickHost.Token);
}
