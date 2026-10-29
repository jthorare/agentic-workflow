using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>The shell builds a hidden page, sheet or dialog the first time it is shown (#1546): a window on Home pays for Home only.</summary>
[Lane(TestLane.Integration)]
public sealed class DeferredPageTests
{
    private static readonly string[] _hidden = ["ContextPage", "JobsPage", "RunPageView", "ItemSheet", "CreateItemDialog"];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Control? Find(ShellWindow window, string name) => window.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == name);

    private static async Task Normal(ShellViewModel shell)
    {
        for (var i = 0; i < 200 && shell.State != ShellState.Normal; i++)
        {
            await Task.Delay(10, Token);
            Dispatcher.UIThread.RunJobs();
        }
    }

    [Fact]
    public async Task A_page_is_built_the_first_time_it_is_shown()
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Token);
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(DeferredPageTests).Assembly).Dispatch(async () =>
        {
            var records = new InMemoryEndpointRecords();
            records.Write(A.Record());
            var shell = new ShellViewModel(records, new FakeSindriApi(), new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
            await shell.Restore(Token);
            var window = new ShellWindow { DataContext = shell };
            try
            {
                window.Show();
                _ = shell.Run(stop.Token);
                await Normal(shell);
                window.UpdateLayout();
                Assert.NotNull(Find(window, "HomePage"));
                Assert.All(_hidden, n => Assert.Null(Find(window, n)));

                shell.GoTo(PageId.Context);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();

                var page = Find(window, "ContextPage");
                Assert.NotNull(page);
                Assert.True(page.IsEffectivelyVisible);
                Assert.Same(shell.Context, page.DataContext);
                Assert.Null(Find(window, "JobsPage"));
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
}
