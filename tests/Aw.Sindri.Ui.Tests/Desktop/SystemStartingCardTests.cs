using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Desktop;

/// <summary>While the app starts Sindri, the System page's Control plane card says what the Home card says, not Not running (#3703).</summary>
[Lane(TestLane.Unit)]
public sealed class SystemStartingCardTests : IDisposable
{
    private readonly InlineUiContext _ui = new();
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly InMemoryPreferencesStore _preferences = new();

    public void Dispose() => _ui.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_Control_plane_card_says_Starting_during_a_start_and_Not_running_after_it_failed()
    {
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(_preferences), new FakeClipboard(), window: WindowPorts.None with { Starter = new StartedStarter() });
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await Eventually(() => shell.Start.Stage == StartStage.Starting && shell.Pill.IsStarting);
        shell.GoTo(PageId.System);
        var during = (shell.Pill.ProcessWord, shell.Pill.ProcessText);

        _clock.Advance(SindriStartViewModel.Wait);
        await Eventually(() => shell.Start.Stage == StartStage.DidntStart && !shell.Pill.IsStarting);

        Assert.Equal(((Labels.StartingWord, Labels.StartingSindri), Labels.NotRunning), (during, shell.Pill.ProcessWord));
    }

    private static async Task Eventually(Func<bool> condition)
    {
        for (var i = 0; i < 400 && !condition(); i++)   // 2 s: the follower runs on the thread pool
        {
            await Task.Delay(5, Token);
        }

        Assert.True(condition());
    }

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
