using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Agents.Page;
using Aw.Sindri.Ui.Views.Agents;

namespace Aw.Sindri.Ui.Tests.Agents.Terminal;

/// <summary>The Agents page draws the terminal view after Enter and takes it away on Esc (agents spec §15.3, U7a).</summary>
[Lane(TestLane.Integration)]
public sealed class AgentsPageTerminalViewTests
{
    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public async Task Enter_puts_a_terminal_view_with_its_pane_in_the_page_and_Esc_takes_it_away()
    {
        var page = await new AgentsPageFixture().Shown(TestContext.Current.CancellationToken);
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(AgentsPageTerminalViewTests).Assembly).Dispatch(() =>
        {
            var window = new Window { Content = new AgentsPage { DataContext = page }, Width = 1440, Height = 900 };
            try
            {
                window.Show();
                Settle(window);
                var before = window.GetVisualDescendants().OfType<TerminalView>().Single().IsEffectivelyVisible;

                _ = page.Press("J");
                _ = page.Press("Enter");
                Settle(window);
                var view = window.GetVisualDescendants().OfType<TerminalView>().Single();
                var hasPane = view.GetVisualDescendants().OfType<Control>().Any(c => string.Equals(c.Name, "Pane", StringComparison.Ordinal));
                var open = view.IsEffectivelyVisible;

                _ = page.Press("Esc");
                Settle(window);

                Assert.False(before);
                Assert.True(open);
                Assert.True(hasPane);
                Assert.False(view.IsEffectivelyVisible);
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
