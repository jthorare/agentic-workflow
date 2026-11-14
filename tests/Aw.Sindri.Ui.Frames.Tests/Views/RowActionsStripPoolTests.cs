using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Headless;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Rows;
using Aw.Sindri.Ui.Views.Controls.Rows;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The action strip pool (#3189): a strip given back keeps its buttons, so a hover on a row does not build new templated buttons.</summary>
[Lane(TestLane.Integration)]
public sealed class RowActionsStripPoolTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(RowActionsStripPoolTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private sealed class Nop : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) { }
    }

    [Fact]
    public Task A_strip_given_back_keeps_its_buttons_and_drops_what_they_run_and_say() => OnUi(() =>
    {
        var strip = RowActionsStrip.Rent();
        strip.Fill([new RowAction("Approve", new Nop()), new RowAction("Hold", new Nop())]);
        var buttons = ((StackPanel)strip.Child!).Children.OfType<Button>().ToList();

        RowActionsStrip.Return(strip);
        var again = RowActionsStrip.Rent();

        Assert.Same(strip, again);
        Assert.Equal(buttons, ((StackPanel)again.Child!).Children.OfType<Button>());   // the same two buttons
        Assert.All(buttons, button => Assert.True(button.Command is null && button.Content is null));
        return true;
    });
}
