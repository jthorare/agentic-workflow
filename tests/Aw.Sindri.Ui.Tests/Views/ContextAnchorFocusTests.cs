using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>#1083 (C-16): opening the Context page puts the cursor in the search box, but not the keyboard focus ring: that is for a person who is tabbing.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ContextAnchorFocusTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly InMemoryPreferencesStore _store = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    [Fact]
    public async Task Opening_the_context_page_focuses_the_search_box_without_the_keyboard_focus_ring()
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(Token);
        bound.CancelAfter(TimeSpan.FromSeconds(30));
        _records.Write(A.Record());
        await new Preferences(_store).Update(p => p with { LastPage = nameof(PageId.Context) });

        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ContextAnchorFocusTests).Assembly).Dispatch(async () =>
        {
            try
            {
                var shell = new ShellViewModel(_records, _server, _clock, new Preferences(_store), new FakeClipboard());
                await shell.Restore(bound.Token);
                var window = new ShellWindow { DataContext = shell };
                _windows.Add(window);
                window.Show();
                _ = shell.Run(_stop.Token);
                for (var i = 0; i < 20; i++)
                {
                    Dispatcher.UIThread.RunJobs();
                }

                var anchor = window.Get<Control>("ContextPage").FindControl<TextBox>("AnchorBox")!;
                Assert.Same(anchor, window.FocusManager?.GetFocusedElement());
                Assert.DoesNotContain(":focus-visible", anchor.Classes, StringComparer.Ordinal);
            }
            finally
            {
                await _stop.CancelAsync();
                _windows.ForEach(w => w.Close());
                Dispatcher.UIThread.RunJobs();
            }

            return true;
        }, bound.Token);
    }
}
