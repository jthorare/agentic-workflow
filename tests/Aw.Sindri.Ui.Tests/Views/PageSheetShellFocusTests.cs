using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context.Whole;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>The page sheet opened through the shell's one sheet puts focus on its Name box once the Deferred template realizes (pages spec §3.6 "Keyboard").</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class PageSheetShellFocusTests : IDisposable
{
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();

    public void Dispose() => _stop.Dispose();

    [Fact]
    public async Task Opening_the_page_sheet_in_the_shell_focuses_Name()
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        bound.CancelAfter(TimeSpan.FromSeconds(30));
        _records.Write(A.Record());
        await new Preferences(new InMemoryPreferencesStore()).Update(p => p with { LastPage = "Context" });

        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(PageSheetShellFocusTests).Assembly).Dispatch(async () =>
        {
            Window? window = null;
            try
            {
                var shell = new ShellViewModel(_records, new FakeSindriApi(), new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
                await shell.Restore(bound.Token);
                window = new ShellWindow { DataContext = shell };
                window.Show();
                _ = shell.Run(_stop.Token);
                Settle();

                var sheet = new PageSheetViewModel(new FakePageWrites(), [], [], openConcept: null);
                shell.Context.Show(sheet);
                Settle();
                await sheet.OpenNew(bound.Token);
                Settle();

                Assert.True(shell.IsPageSheet);
                var focused = window.FocusManager?.GetFocusedElement() as Control;
                Assert.Equal("PageSheetName", focused?.Name);
                Assert.NotNull(window.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(t => t.Name == "PageSheetName"));
            }
            finally
            {
                await _stop.CancelAsync();
                window?.Close();
                Dispatcher.UIThread.RunJobs();
            }

            return true;
        }, bound.Token);
    }

    private static void Settle()
    {
        for (var i = 0; i < 20; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }
}
