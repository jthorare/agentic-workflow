using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Rendering.Tests.Views;

/// <summary>UX pass C-4 (and B-4): a control that hides itself while it holds keyboard focus hands focus to the control that takes over, so no shortcut is left without a focused element.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class FocusHandOffTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(FocusHandOffTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    [Fact]
    public async Task A_focused_control_that_leaves_hands_focus_to_the_next()
    {
        var (nextFocused, nothingFocused) = await OnUi(() =>
        {
            var next = new TextBox();
            var leaving = new Button { Content = "Clear" };
            var window = new Window { Content = new StackPanel { Children = { next, leaving } }, Width = 300, Height = 200 };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            _ = leaving.Focus();

            FocusHandOff.Before(leaving, next);
            leaving.IsVisible = false;
            Dispatcher.UIThread.RunJobs();

            return (next.IsFocused, window.FocusManager?.GetFocusedElement() is null);
        });

        Assert.True(nextFocused);
        Assert.False(nothingFocused);
    }

    [Fact]
    public async Task A_control_that_does_not_hold_focus_leaves_focus_where_it_is()
    {
        var elsewhereFocused = await OnUi(() =>
        {
            var elsewhere = new TextBox();
            var leaving = new Button { Content = "Clear" };
            var next = new TextBox();
            var window = new Window { Content = new StackPanel { Children = { elsewhere, leaving, next } }, Width = 300, Height = 200 };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            _ = elsewhere.Focus();

            FocusHandOff.Before(leaving, next);

            return elsewhere.IsFocused;
        });

        Assert.True(elsewhereFocused);
    }
}
