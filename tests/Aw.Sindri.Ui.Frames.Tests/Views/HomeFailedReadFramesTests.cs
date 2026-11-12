using System.Net;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>#3056: Home draws a failed queue read as the shared failed line with Retry, in both panels, and the empty sentences stay hidden.</summary>
[Lane(TestLane.Integration)]
public sealed class HomeFailedReadFramesTests : FrameHost
{
    [Fact]
    public Task A_failed_queue_read_shows_the_failed_line_with_retry_and_no_empty_sentence() => OnUi(async () =>
    {
        Server.QueueView = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-011", "The views didn't catch up within 2 s (fix: retry)");
        var (window, shell) = await Connected();
        shell.GoTo(PageId.Home);
        Settle(window);

        Assert.True(Visible(window, "RunningFailed"));
        Assert.True(Visible(window, "QueuedFailed"));
        Assert.False(Visible(window, "RunningEmpty"));
        Assert.False(Visible(window, "QueuedEmpty"));
        var words = Named<TextBlock>(Named<Control>(window, "RunningFailed"), "FailedReadWords");
        Assert.DoesNotContain("SND-", words.Text, StringComparison.Ordinal);

        Server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p4-waiting");
        Named<Button>(Named<Control>(window, "RunningFailed"), "FailedReadPrimary").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(window, () => !shell.Queue.HasProblem);

        Assert.False(Visible(window, "RunningFailed"));
        Assert.False(Visible(window, "QueuedFailed"));
    });
}
