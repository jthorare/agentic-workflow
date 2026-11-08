using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.ViewModels.Toast;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The shell's toast (todo spec §3.3, §3.5): it draws its text and Undo over the page, and its polite region speaks the key.</summary>
[Lane(TestLane.Integration)]
public sealed class ToastHostTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };
    private readonly OffThreadTimers _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ToastHostTests).Assembly)
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

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static T Named<T>(Visual within, string name)
        where T : Control => within.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Running()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell };
        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        for (var i = 0; i < 300 && shell.State != ShellState.Normal; i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        Assert.Equal(ShellState.Normal, shell.State);
        return (window, shell);
    }

    [Fact]
    public Task The_toast_draws_its_text_and_Undo_and_its_live_region_names_the_key() => OnUi(async () =>
    {
        var (window, shell) = await Running();
        var host = Named<ToastHost>(window, "Toast");
        var live = Named<TextBlock>(window, "ToastLive");
        Assert.False(host.IsEffectivelyVisible);

        shell.ShowToast(new ToastMessage("Done.", () => Task.CompletedTask));
        Settle(window);

        Assert.True(host.IsEffectivelyVisible);
        Assert.Equal("Done.", Named<TextBlock>(window, "ToastText").Text);
        Assert.True(Named<Button>(window, "ToastUndo").IsEffectivelyVisible);
        Assert.Equal("Done. Undo with Control Z", live.Text);
        Assert.Equal(Avalonia.Automation.AutomationLiveSetting.Polite, Avalonia.Automation.AutomationProperties.GetLiveSetting(live));
        Assert.True(host.Bounds.Height > 0 && host.Bounds.Bottom <= window.Bounds.Height);   // drawn inside the window, at the bottom of the page

        shell.Toast.PressEscape(focusOnToast: true);
        Settle(window);
        Assert.False(host.IsEffectivelyVisible);
    });
}
