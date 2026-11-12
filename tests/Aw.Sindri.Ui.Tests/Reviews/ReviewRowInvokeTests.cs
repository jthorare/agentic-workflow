using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Tests.Views;

namespace Aw.Sindri.Ui.Tests.Reviews;

/// <summary>A PR row on the Reviews list (issue #2791): screen readers can invoke it, and invoking it opens the PR.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ReviewRowInvokeTests : FrameHost
{
    [Fact]
    public Task ReviewRow_exposes_invoke_and_opens_the_PR() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        shell.GoTo(PageId.Reviews);
        await Until(window, () => !shell.Reviews.IsLoading, "the Reviews page never read the list");
        var row = window.GetVisualDescendants().OfType<Border>().First(b => b.Name == "ReviewRowFrame" && b.IsEffectivelyVisible);

        var invoke = ControlAutomationPeer.CreatePeerForElement(row).GetProvider<IInvokeProvider>();

        Assert.NotNull(invoke);
        invoke.Invoke();
        await Until(window, () => shell.IsReviewPageShown, "invoking the row never opened the PR");
    });
}
