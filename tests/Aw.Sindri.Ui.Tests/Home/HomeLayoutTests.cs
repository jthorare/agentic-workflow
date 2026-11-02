using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Layout;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Home;

/// <summary>Home's regions (F3 home spec §2): Needs you on the left, Running and Queued on the right, the health strip across the bottom; under 1100 px the columns stack.</summary>
[Lane(TestLane.Integration)]
public sealed class HomeLayoutTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task OnHome(double width, Action<ShellWindow> check, PageTier? tier = null)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Token);
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(HomeLayoutTests).Assembly).Dispatch(async () =>
        {
            var records = new InMemoryEndpointRecords();
            records.Write(A.Record());
            var shell = new ShellViewModel(records, new FakeSindriApi { Catalog = RouteAnswer.Api("commands", "catalog") }, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
            await shell.Restore(Token);
            var window = new ShellWindow { DataContext = shell, Width = width };
            try
            {
                window.Show();
                _ = shell.Run(stop.Token);
                for (var i = 0; i < 300 && !(shell.State == ShellState.Normal && shell.Queue.Groups.Count > 0); i++)
                {
                    await Task.Delay(10, Token);
                    Dispatcher.UIThread.RunJobs();
                }

                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                if (tier is { } forced)
                {
                    // the window cannot be narrower than 1000 px, so the Narrow tier (page region under 1000 px, e.g. beside an open sheet) is set on the page region itself
                    PageLayout.SetTier(window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "Page"), forced);
                }

                window.UpdateLayout();
                check(window);
            }
            finally
            {
                await stop.CancelAsync();
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }

            return true;
        }, Token);
    }

    private static Rect Box(Window window, string name)
    {
        var control = window.GetVisualDescendants().OfType<Control>().Single(c => c.Name == name);
        return new Rect(control.TranslatePoint(default, window) ?? default, control.Bounds.Size);
    }

    [Fact]
    public async Task Wide_needs_you_is_the_left_column_and_the_rest_sit_to_its_right()
    {
        await OnHome(1440, window =>
        {
            Rect needs = Box(window, "NeedsYouPanel"), running = Box(window, "WorkflowsPanel"), queued = Box(window, "QueuedPanel"), health = Box(window, "HealthStrip");

            Assert.True(running.Left >= needs.Right, "Running should sit to the right of Needs you");
            Assert.True(queued.Left >= needs.Right, "Queued should sit to the right of Needs you");
            Assert.True(Math.Abs(needs.Top - running.Top) < 1, "the two columns start on the same line");
            Assert.True(queued.Top >= running.Bottom - 0.5, "Queued sits under Running");
            Assert.True(health.Top >= Math.Max(needs.Bottom, queued.Bottom) - 0.5, "the health strip runs across the bottom");
        });
    }

    [Fact]
    public async Task At_the_Narrow_tier_the_columns_stack_with_needs_you_first_each_the_full_width()
    {
        await OnHome(1000, window =>
        {
            Rect needs = Box(window, "NeedsYouPanel"), running = Box(window, "WorkflowsPanel"), queued = Box(window, "QueuedPanel");

            Assert.True(running.Top >= needs.Bottom - 0.5, "Running sits under Needs you");
            Assert.True(queued.Top >= running.Bottom - 0.5, "Queued sits under Running");
            Assert.True(Math.Abs(needs.Width - running.Width) < 1 && Math.Abs(needs.Left - running.Left) < 1, "the panels share one full-width column");
        }, PageTier.Narrow);
    }
}
