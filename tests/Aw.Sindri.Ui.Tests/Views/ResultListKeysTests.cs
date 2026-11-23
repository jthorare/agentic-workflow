using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>Arrow keys on a result list (#3677, decision 4): Down and Up move between the rows, stop at the ends, and Enter presses the focused row.</summary>
[Lane(TestLane.Integration)]
public sealed class ResultListKeysTests
{
    private static readonly string[] _names = ["one", "two", "three"];

    private static (Window Window, List<Button> Rows, List<string> Pressed) Show()
    {
        List<string> pressed = [];
        var list = new ItemsControl { ItemsSource = _names };
        ResultListKeys.SetArrows(list, value: true);
        list.ItemTemplate = new FuncDataTemplate<string>((name, _) =>
        {
            var row = new Button { Content = name };
            row.Classes.Add("result-open");
            row.Click += (_, _) => pressed.Add(name);
            return row;
        });
        var window = new Window { Content = list, Width = 400, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return (window, [.. list.GetVisualDescendants().OfType<Button>()], pressed);
    }

    private static void Press(Window window, Key key, PhysicalKey physical)
    {
        window.KeyPress(key, RawInputModifiers.None, physical, keySymbol: null);
        Dispatcher.UIThread.RunJobs();
    }

    private static string? Focused(Window window) => (window.FocusManager?.GetFocusedElement() as Button)?.Content as string;

    [Fact]
    public async Task Down_and_Up_move_between_the_rows_stop_at_the_ends_and_Enter_presses_the_focused_one()
    {
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ResultListKeysTests).Assembly).Dispatch(() =>
        {
            var (window, rows, pressed) = Show();
            try
            {
                _ = rows[0].Focus(NavigationMethod.Tab);
                Press(window, Key.Down, PhysicalKey.ArrowDown);
                Press(window, Key.Down, PhysicalKey.ArrowDown);
                var atEnd = Focused(window);
                Press(window, Key.Down, PhysicalKey.ArrowDown);
                var stillEnd = Focused(window);
                Press(window, Key.Up, PhysicalKey.ArrowUp);
                var back = Focused(window);
                Press(window, Key.Enter, PhysicalKey.Enter);

                Assert.Equal(("three", "three", "two"), (atEnd, stillEnd, back));
                Assert.Equal(["two"], pressed);
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }

            return true;
        }, TestContext.Current.CancellationToken);
    }
}
