using System.Net;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.NeedsYou;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>#2921: when the needs-you read fails, Home's Needs you card shows one failed line and a Retry button, never the loading skeleton.</summary>
[Lane(TestLane.Integration)]
public sealed class NeedsYouReadFailedTests : FrameHost
{
    private const string FailedText = "Couldn't load what needs you";

    [Theory]
    [InlineData(1280)]
    [InlineData(1000)]
    public Task A_failed_needs_you_read_shows_a_failed_line_and_retry_not_the_skeleton(double width) => OnUi(async () =>
    {
        Server.NeedsYouView = RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-TEST-500", "boom");

        var (window, shell) = await Connected(width: width, height: 900);
        shell.GoTo(PageId.Home);
        Settle(window);

        var home = window.GetVisualDescendants().OfType<HomePage>().Single(h => h.IsEffectivelyVisible);
        Assert.DoesNotContain(home.GetVisualDescendants().OfType<Control>(), c => c.IsEffectivelyVisible && c.Classes.Contains("skeleton"));
        Assert.Contains(home.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == FailedText);
        Assert.Contains(home.GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible && b.Name == "NeedsYouRetry");
    });

    [Fact]
    public Task Retry_after_a_failed_read_shows_the_rows() => OnUi(async () =>
    {
        Server.NeedsYouView = RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-TEST-500", "boom");

        var (window, shell) = await Connected(width: 1280, height: 900);
        shell.GoTo(PageId.Home);
        Settle(window);

        var retry = window.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.IsEffectivelyVisible && b.Name == "NeedsYouRetry");
        Assert.NotNull(retry);

        Server.NeedsYouView = RouteAnswer.Api("needs-you", "fixture");   // the FakeSindriApi default
        retry.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);

        Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == FailedText);
        Assert.Contains(window.GetVisualDescendants().OfType<NeedsYouRow>(), r => r.IsEffectivelyVisible);
    });
}
