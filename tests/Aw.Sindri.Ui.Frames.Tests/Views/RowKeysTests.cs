using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>RowKeys on a plain 200-row virtualized list that is not the item sheet: the keys walk rows by index and never leave the list (#2922).</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class RowKeysTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(RowKeysTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static (Window Window, ItemsControl List) Show()
    {
        var list = new ItemsControl
        {
            ItemsSource = Enumerable.Range(0, 200).Select(i => "Row " + i.ToString(CultureInfo.InvariantCulture)).ToList(),
            ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel()),
            ItemTemplate = new FuncDataTemplate<string>((text, _) => new Border { Focusable = true, Height = 24, Tag = text, Child = new TextBlock { Text = text } }),
        };
        RowKeys.SetEnabled(list, value: true);
        var window = new Window { Content = new ScrollViewer { Content = list }, Width = 400, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, list);
    }

    private static void Press(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    private static string Focused(Window window) => (window.FocusManager?.GetFocusedElement() as Control)?.Tag as string ?? "outside the list";

    [Fact]
    public async Task Down_from_the_first_row_moves_to_the_second() => await OnUi(() =>
    {
        var (window, list) = Show();
        RowKeys.FocusRow(list, 0);

        Press(window, PhysicalKey.ArrowDown);

        Assert.Equal("Row 1", Focused(window));
        window.Close();
        return true;
    });

    [Fact]
    public async Task End_builds_and_focuses_the_last_row_though_it_started_unbuilt() => await OnUi(() =>
    {
        var (window, list) = Show();
        RowKeys.FocusRow(list, 0);
        Assert.Null(list.ContainerFromIndex(199));

        Press(window, PhysicalKey.End);

        Assert.Equal("Row 199", Focused(window));
        window.Close();
        return true;
    });

    [Fact]
    public async Task Home_returns_to_the_first_row() => await OnUi(() =>
    {
        var (window, list) = Show();
        RowKeys.FocusRow(list, 150);

        Press(window, PhysicalKey.Home);

        Assert.Equal("Row 0", Focused(window));
        window.Close();
        return true;
    });

    [Fact]
    public async Task Up_at_the_first_row_stays_on_it() => await OnUi(() =>
    {
        var (window, list) = Show();
        RowKeys.FocusRow(list, 0);

        Press(window, PhysicalKey.ArrowUp);

        Assert.Equal("Row 0", Focused(window));
        window.Close();
        return true;
    });
}
