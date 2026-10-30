using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Keys.Tests.Views;

/// <summary>The `?` sheet is modal (UI spec §11.2): Tab and Shift+Tab never leave it for the page behind the scrim (#1081, C-15).</summary>
[Lane(TestLane.Integration)]
public sealed class ShortcutsSheetFocusTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ShortcutsSheetFocusTests).Assembly)
            .Dispatch(async () =>
            {
                try
                {
                    await body();
                }
                finally
                {
                    await _stop.CancelAsync();
                    _windows.ForEach(w => w.Close());
                    Dispatcher.UIThread.RunJobs();
                }

                return true;
            }, Token);

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Connected()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell };
        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        for (var i = 0; i < 200 && !(shell.State == ShellState.Normal && shell.Health.Catalog.Controls.Count > 0); i++)
        {
            await Task.Delay(10, Token);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal(ShellState.Normal, shell.State);
        Settle(window);
        return (window, shell);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static bool InsideSheet(Window window)
    {
        var card = window.GetVisualDescendants().OfType<Control>().First(c => c.Name == "ShortcutsCard");
        var focused = window.FocusManager?.GetFocusedElement() as Visual;
        return focused is not null && (focused == card || card.IsVisualAncestorOf(focused));
    }

    [Theory]
    [InlineData(RawInputModifiers.None)]
    [InlineData(RawInputModifiers.Shift)]
    public Task Tab_presses_past_the_last_stop_stay_inside_the_Shortcuts_sheet(RawInputModifiers modifiers) => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        window.KeyPress(Key.OemQuestion, RawInputModifiers.Shift, PhysicalKey.Slash, "?");
        Settle(window);
        Assert.True(shell.Shortcuts.IsOpen);
        Assert.True(InsideSheet(window), "the sheet takes focus when it opens");

        for (var press = 1; press <= 6; press++)
        {
            window.KeyPress(Key.Tab, modifiers, PhysicalKey.Tab, keySymbol: null);
            Settle(window);
            Assert.True(InsideSheet(window), string.Create(System.Globalization.CultureInfo.InvariantCulture, $"focus left the sheet on Tab press {press}"));
        }
    });
}
