using System.Globalization;
using System.Net;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// #2921: Needs you's failed read uses the app's shared failed-read layout, the one ResourcesFailed, WorkflowsFailed and MemoryFailed use: a vertical
/// stack, the failed line as a subtle caption (not .quiet, which centres) inside a Border that carries its screen-reader name, then a plain left-aligned Retry button.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class NeedsYouFailedLayoutTests : FrameHost
{
    private const string FailedText = "Couldn't load what needs you";

    [Theory]
    [InlineData(1280)]
    [InlineData(1000)]
    public Task The_failed_line_uses_the_shared_failed_read_layout(double width) => OnUi(async () =>
    {
        Server.NeedsYouView = RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-TEST-500", "boom");

        var (window, shell) = await Connected(width: width, height: 900);
        shell.GoTo(PageId.Home);
        Settle(window);
        // Saved before the asserts, so a red run still leaves a frame to look at (qa on #2950).
        FrameFile.Save(window, string.Create(CultureInfo.InvariantCulture, $"h2921-needs-you-failed-{width}"));

        var home = window.GetVisualDescendants().OfType<HomePage>().Single(h => h.IsEffectivelyVisible);
        var failed = home.GetVisualDescendants().OfType<StackPanel>().Single(p => p.Name == "NeedsYouFailed");
        Assert.True(failed.IsEffectivelyVisible);
        Assert.Equal(Orientation.Vertical, failed.Orientation);

        var named = Assert.IsType<Border>(failed.Children[0]);
        Assert.Equal(FailedText, AutomationProperties.GetName(named));
        var line = Assert.IsType<TextBlock>(named.Child);
        Assert.Equal(FailedText, line.Text);
        Assert.Contains("caption", line.Classes, StringComparer.Ordinal);
        Assert.Contains("subtle", line.Classes, StringComparer.Ordinal);
        Assert.DoesNotContain("quiet", line.Classes, StringComparer.Ordinal);

        var retry = Assert.IsType<Button>(failed.Children[1]);
        Assert.Equal("NeedsYouRetry", retry.Name);
        Assert.DoesNotContain("copy", retry.Classes, StringComparer.Ordinal);
        Assert.Equal(HorizontalAlignment.Left, retry.HorizontalAlignment);

        // The caption starts on the card's text column, the 24 the count line, banner and empty line use (qa on #2950).
        var card = Assert.IsType<Visual>(failed.GetVisualParent(), exactMatch: false);
        var left = Assert.NotNull(line.TranslatePoint(default, card));
        Assert.Equal(24, left.X, precision: 1);
        window.Close();
    });
}
