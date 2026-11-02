using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Theme;
using Aw.Sindri.Ui.Tests.Views;

namespace Aw.Sindri.Ui.Controls.Tests.Views;

/// <summary>A focused primary button in a row keeps a ring that contrasts (review run 4, a11y): the in-row ring is at least 3:1 against the primary fill and against the row, and differs from the unfocused look (WCAG 1.4.11).</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class PrimaryRowFocusTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Hex(IBrush? brush) => brush is ISolidColorBrush { Color: var c } ? $"#{c.R:x2}{c.G:x2}{c.B:x2}" : throw new InvalidOperationException("not a solid brush");

    private static ContentPresenter Presenter(Button button)
    {
        button.ApplyTemplate();
        return button.GetVisualDescendants().OfType<ContentPresenter>().First(p => p.Name == "PART_ContentPresenter");
    }

    [Fact]
    public async Task A_focused_primary_row_button_rings_at_three_to_one_against_its_fill_and_the_row()
    {
        var (rest, focused, fill) = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(PrimaryRowFocusTests).Assembly)
            .Dispatch(() =>
            {
                var button = new Button { Content = "Accept" };
                button.Classes.Add("command-control");
                button.Classes.Add("primary");
                var row = new Border { Child = button };
                row.Classes.Add("item-row");
                var window = new Window { Content = row };
                window.Show();
                Dispatcher.UIThread.RunJobs();
                var presenter = Presenter(button);
                var unfocused = Hex(presenter.BorderBrush);
                _ = button.Focus(NavigationMethod.Tab);
                Dispatcher.UIThread.RunJobs();
                var result = (unfocused, Hex(presenter.BorderBrush), Hex(presenter.Background));
                window.Close();
                return result;
            }, Token);

        var tokens = TokenFile.Load();
        Assert.NotEqual(rest, focused, StringComparer.Ordinal);
        Assert.True(Wcag.Contrast(focused, fill) >= 3, "ring " + focused + " on fill " + fill);
        Assert.True(Wcag.Contrast(focused, tokens.Color("Surface1")) >= 3, "ring " + focused + " on the row");
    }
}
