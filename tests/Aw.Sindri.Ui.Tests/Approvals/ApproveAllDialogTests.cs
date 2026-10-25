using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Approvals;
using Aw.Sindri.Ui.Views.Approvals;

namespace Aw.Sindri.Ui.Tests.Approvals;

/// <summary>The approve-all dialog's view: one click approves, and focus follows the dialog.</summary>
[Lane(TestLane.Integration)]
public sealed class ApproveAllDialogTests
{
    private readonly ApproveAllFixture _f = new();

    private static CancellationToken Token => ApproveAllFixture.Token;

    private static async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ApproveAllDialogTests).Assembly)
            .Dispatch(async () =>
            {
                await body();
                return true;
            }, Token);

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static T Named<T>(Visual within, string name)
        where T : Control => within.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static Control? Focused(Window window) => window.FocusManager?.GetFocusedElement() as Control;

    private async Task<(Window Window, ApproveAllDialog Dialog, ApproveAllViewModel Model)> Shown()
    {
        var model = await _f.Dialog();
        var dialog = new ApproveAllDialog { DataContext = model };
        var window = new Window { Width = 900, Height = 900, Content = dialog };
        window.Show();
        dialog.FocusFirst();
        await model.Open(Token);
        Settle(window);
        return (window, dialog, model);
    }

    private static Point CenterOf(Window window, Control control) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window) ?? throw new InvalidOperationException("the control is not in the window");

    [Fact]
    public Task One_click_on_Approve_sends_the_approval_once_with_no_challenge() => OnUi(async () =>
    {
        var (window, _, model) = await Shown();
        var approve = Named<Button>(window, "ApproveButton");
        var on = CenterOf(window, approve);

        window.MouseDown(on, MouseButton.Left);
        window.MouseUp(on, MouseButton.Left);
        Settle(window);
        await WaitFor(() => model.Phase == ApproveAllPhase.Done);

        _ = Assert.Single(_f.Server.Approvals);
        window.Close();
    });

    [Fact]
    public Task The_approve_button_takes_focus_when_the_plan_arrives_from_Close() => OnUi(async () =>
    {
        var (window, _, _) = await Shown();

        Assert.Same(Named<Button>(window, "ApproveButton"), Focused(window));
        window.Close();
    });

    [Fact]
    public Task Focus_goes_to_Close_when_the_outcome_appears() => OnUi(async () =>
    {
        var (window, _, model) = await Shown();

        await _f.Finish(model.Approve());
        Settle(window);

        Assert.Equal((ApproveAllPhase.Done, "ApproveClose"), (model.Phase, Focused(window)?.Name));
        window.Close();
    });

    private async Task WaitFor(Func<bool> condition)
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(Token);
        bound.CancelAfter(TimeSpan.FromSeconds(20));
        while (!condition())
        {
            _f.Clock.Advance(TimeSpan.FromMilliseconds(500));
            await Task.Delay(1, bound.Token);
        }
    }
}
