using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Icons;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// The Starting card and pill under the headless platform (first-run spec 4.1; #3663, #3672): at the 1280 px default and the
/// 1000 px minimum the busy mark sits left of the heading and the pill's word, inside the window, and the details line shows.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class StartingCardFramesTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly InMemoryPreferencesStore _preferences = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    [Fact]
    public Task The_busy_marks_sit_left_of_the_heading_and_the_pill_word_at_both_widths() => OnUi(async () =>
    {
        foreach (var width in new[] { 1280d, 1000d })
        {
            var window = await Show(width);

            var heading = Named<TextBlock>(window, "NotRunningText");
            var spinner = Named<StateIcon>(window, "StartingSpinner");
            var pillWord = Named<TextBlock>(window, "HealthPillText");
            var pillSpinner = Named<StateIcon>(window, "PillSpinner");

            Assert.Equal(("Starting", "Starting"), (heading.Text, pillWord.Text));
            Assert.True(Left(window, spinner) < Left(window, heading), "the card's busy mark isn't left of its heading");
            Assert.True(Left(window, pillSpinner) < Left(window, pillWord), "the pill's busy mark isn't left of its word");
            Assert.True(Named<TextBlock>(window, "StartDetailsText").IsEffectivelyVisible, "the details line doesn't show");
            Assert.True(Right(window, Named<Border>(window, "NotRunningPanel")) <= window.Bounds.Width, "the card runs past the window");
        }
    });

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(StartingCardFramesTests).Assembly)
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

    private async Task<ShellWindow> Show(double width)
    {
        await new Preferences(_preferences).Update(_ => UiPreferences.Defaults with { Window = new WindowBounds(0, 0, width, 600) });
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(_preferences), new FakeClipboard(), window: WindowPorts.None with { Starter = new StartedStarter() });
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell };
        _windows.Add(window);
        window.Show();
        var following = shell.Run(_stop.Token);
        for (var i = 0; i < 200 && shell.Start.Stage != StartStage.Starting && !following.IsFaulted; i++)
        {
            await Task.Delay(10, Token);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal(StartStage.Starting, shell.Start.Stage);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static T Named<T>(Window window, string name)
        where T : Control => window.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static double Left(Window window, Control control) => control.TranslatePoint(default, window)?.X ?? double.NaN;

    private static double Right(Window window, Control control) => Left(window, control) + control.Bounds.Width;

    private sealed class StartedStarter : ISindriStarter
    {
        public event EventHandler<SindriExitedEventArgs>? Exited { add { } remove { } }

        public int Launch => 1;

        public string LogPath => "/logs/serve.log";

        public void Release()
        {
        }

        public string? LastError() => null;

        public Result<SindriStartOutcome> Start() => SindriStartOutcome.Started;
    }
}
