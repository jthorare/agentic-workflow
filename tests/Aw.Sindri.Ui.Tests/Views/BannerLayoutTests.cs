using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Icons;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// The Unreadable banner's layout under the headless platform (UI spec §4.1; prover run 7): at the 1280 px default and
/// the 1000 px minimum a long reason wraps, and the icon, the heading, the reason, "Check it:" and the sindri doctor box
/// all stay inside the window.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class BannerLayoutTests : IDisposable
{
    private const string LongReason = "/sindri/status answered with something this build can't read, and this sentence runs on "
        + "long enough that no window the app allows can show it on one line, so the banner has to wrap it and keep the fix in view";

    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly InMemoryPreferencesStore _preferences = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(BannerLayoutTests).Assembly)
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

    private async Task<ShellWindow> Show(double width, Func<ShellViewModel, bool> ready)
    {
        await new Preferences(_preferences).Update(_ => UiPreferences.Defaults with { Window = new WindowBounds(0, 0, width, 600) });
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(_preferences), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell };
        _windows.Add(window);
        window.Show();
        var following = shell.Run(_stop.Token);
        for (var i = 0; i < 200 && !ready(shell) && !following.IsFaulted; i++)
        {
            await Task.Delay(10, Token);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.False(following.IsFaulted, following.Exception?.ToString());
        Assert.True(ready(shell), "the shell never reached the expected state");
        Settle(window);
        Assert.Equal(width, window.Bounds.Width);
        return window;
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static T Named<T>(Window window, string name)
        where T : Control => window.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    // The control's box in the window lies inside the window, and it shows (it has a size).
    private static void Inside(Window window, Control control, string what)
    {
        var box = new Rect(control.TranslatePoint(default, window)!.Value, control.Bounds.Size); // why: the control is in the window
        Assert.True(control.IsEffectivelyVisible && box.Width > 0 && box.Height > 0, what + " doesn't show");
        Assert.True(box.Left >= 0 && box.Right <= window.Bounds.Width + 0.5 && box.Bottom <= window.Bounds.Height + 0.5,
            $"{what} runs out of the window: {box} in {window.Bounds.Size}");
    }

    private static void Frame(Window window, string name)
    {
        Settle(window);
        FrameFile.Save(window, "shell-" + name);
    }

    [Theory]
    [InlineData(1280)]
    [InlineData(1000)]
    public Task Unreadable_wraps_a_long_reason_and_keeps_sindri_doctor_in_the_window(double width) => OnUi(async () =>
    {
        _records.Write(A.Record());
        _server.UnreadableStatus = true;
        _server.UnreadableReason = LongReason;
        var window = await Show(width, s => s.State == ShellState.Unreadable);

        var banner = Named<Border>(window, "UnreadableBanner");
        var reason = Named<TextBlock>(window, "UnreadableText");
        Assert.True(reason.TextLayout.TextLines.Count > 1, string.Create(CultureInfo.InvariantCulture, $"the reason is on {reason.TextLayout.TextLines.Count} line at {width} px"));
        Inside(window, banner.GetVisualDescendants().OfType<StateIcon>().First(), "the icon");
        Inside(window, Named<TextBlock>(window, "UnreadableHeading"), "the heading");
        Inside(window, reason, "the reason");
        Inside(window, Named<TextBlock>(window, "UnreadableCheckIt"), "Check it:");
        var fix = Named<TextBlock>(window, "DoctorFixText");
        Inside(window, fix.FindAncestorOfType<Border>()!, "the sindri doctor box"); // why: the command sits in its box
        Inside(window, fix, "sindri doctor");
        Assert.False(fix.TextLayout.TextLines.Any(l => l.HasCollapsed), "sindri doctor is trimmed");
        Frame(window, string.Create(CultureInfo.InvariantCulture, $"unreadable-long-{width:0}"));
    });
}
