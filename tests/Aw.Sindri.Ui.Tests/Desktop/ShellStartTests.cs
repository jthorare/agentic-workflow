using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Desktop;

/// <summary>The shell starts Sindri only when the app opens with it down (first-run decision 2a, #3643): a later outage shows the Start Sindri button.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellStartTests : IDisposable
{
    private readonly InlineUiContext _ui = new();
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly InMemoryPreferencesStore _preferences = new();

    public void Dispose() => _ui.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sindri_stopping_after_the_app_opened_with_it_up_starts_nothing_and_shows_the_button()
    {
        var starter = new CountingStarter();
        _records.Write(A.Record());
        _server.Current = A.Status();
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(_preferences), new FakeClipboard(), window: WindowPorts.None with { Starter = starter });
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await Eventually(() => shell.State == ShellState.Normal);

        _records.Remove();
        _clock.Advance(HealthViewModel.RetryEvery);
        await Eventually(() => shell.State == ShellState.NotRunning);

        Assert.Equal((0, StartStage.Idle, "Start Sindri"), (starter.Starts, shell.Start.Stage, shell.Start.Primary));
    }

    [Fact]
    public async Task The_pill_and_heading_say_Starting_while_a_start_runs()
    {
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(_preferences), new FakeClipboard(), window: WindowPorts.None with { Starter = new CountingStarter() });
        await shell.Restore(Token);
        _ = shell.Run(Token);

        await Eventually(() => shell.Start.Stage == StartStage.Starting && shell.Pill.IsStarting);

        Assert.Equal((Labels.StartingWord, StateGlyph.None, false, Labels.StartingWord), (shell.Pill.Word, shell.Pill.Glyph, shell.Pill.IsDown, shell.Start.Heading));
    }

    [Fact]
    public async Task Not_running_with_the_red_mark_shows_only_after_a_failed_start()
    {
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(_preferences), new FakeClipboard(), window: WindowPorts.None with { Starter = new CountingStarter() });
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await Eventually(() => shell.Start.Stage == StartStage.Starting);

        _clock.Advance(SindriStartViewModel.Wait);
        await Eventually(() => shell.Start.Stage == StartStage.DidntStart && !shell.Pill.IsStarting);

        Assert.Equal((Labels.NotRunning, StateGlyph.Down, true, Labels.NotRunning), (shell.Pill.Word, shell.Pill.Glyph, shell.Pill.IsDown, shell.Start.Heading));
    }

    private static async Task Eventually(Func<bool> condition)
    {
        for (var i = 0; i < 400 && !condition(); i++)   // 2 s: the follower runs on the thread pool
        {
            await Task.Delay(5, Token);
        }

        Assert.True(condition());
    }

    private sealed class CountingStarter : ISindriStarter
    {
        public event EventHandler<SindriExitedEventArgs>? Exited { add { } remove { } }

        public int Starts { get; private set; }

        public int Launch => Starts;

        public string LogPath => "/logs/serve.log";

        public void Release()
        {
        }

        public string? LastError() => null;

        public Result<SindriStartOutcome> Start()
        {
            Starts++;
            return SindriStartOutcome.Started;
        }
    }
}
