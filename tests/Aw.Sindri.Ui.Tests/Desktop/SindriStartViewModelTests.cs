using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Desktop;

/// <summary>The Not running page's start states (first-run spec §4.1, decision 2a; issue 3326).</summary>
[Lane(TestLane.Unit)]
public sealed class SindriStartViewModelTests : IDisposable
{
    private readonly InlineUiContext _ui = new();

    public void Dispose() => _ui.Dispose();

    [Fact]
    public void Begin_starts_sindri_once_and_shows_Starting()
    {
        var starter = new FakeStarter();
        var model = new SindriStartViewModel(starter, new FakeTimeProvider());

        model.Begin();
        model.Begin();

        Assert.Equal((1, StartStage.Starting, "Starting Sindri…", false), (starter.Starts, model.Stage, model.Message, model.HasPrimary));
    }

    [Fact]
    public void Nothing_answering_for_30_s_shows_the_last_error_with_Retry()
    {
        var clock = new FakeTimeProvider();
        var starter = new FakeStarter { Error = "SND-CLI-002 exec /x/sindri: denied" };
        var model = new SindriStartViewModel(starter, clock);
        model.Begin();

        clock.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal((StartStage.DidntStart, "Sindri didn't start: exec /x/sindri: denied.", "Retry", "/logs/serve.log · sindri serve"), (model.Stage, model.Message, model.Primary, model.Details));
    }

    [Fact]
    public void A_refused_second_serve_shows_Already_starting_without_a_button()
    {
        var clock = new FakeTimeProvider();
        var model = new SindriStartViewModel(new FakeStarter { Error = "SND-LEASE-001 serve is already running" }, clock);
        model.Begin();

        clock.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal((StartStage.AlreadyStarting, "Sindri is already starting on this machine.", false), (model.Stage, model.Message, model.HasPrimary));
    }

    [Fact]
    public void A_missing_binary_shows_the_install_text_with_no_button()
    {
        var model = new SindriStartViewModel(new FakeStarter { Outcome = SindriStartOutcome.NotInstalled }, new FakeTimeProvider());

        model.Begin();

        Assert.Equal((StartStage.NotInstalled, "The Sindri program isn't installed next to this app.", "scripts/install-cs.sh --link", false), (model.Stage, model.Message, model.Details, model.HasPrimary));
    }

    [Fact]
    public void Sindri_answering_before_30_s_resets_the_page_and_no_failure_shows_later()
    {
        var clock = new FakeTimeProvider();
        var model = new SindriStartViewModel(new FakeStarter(), clock);
        model.Begin();

        model.Reset();
        clock.Advance(TimeSpan.FromSeconds(31));

        Assert.Equal((StartStage.Idle, "Start Sindri"), (model.Stage, model.Primary));
    }

    [Fact]
    public void Serve_reached_releases_the_start_so_a_later_Start_launches_again()
    {
        var starter = new FakeStarter();
        var model = new SindriStartViewModel(starter, new FakeTimeProvider());
        model.Begin();
        model.Reset();

        model.Start();

        Assert.Equal((2, 1), (starter.Starts, starter.Releases));
    }

    [Fact]
    public void Retry_starts_again_and_the_first_wait_no_longer_fires()
    {
        var clock = new FakeTimeProvider();
        var starter = new FakeStarter { Error = "SND-CLI-002 exec: x" };
        var model = new SindriStartViewModel(starter, clock);
        model.Begin();
        clock.Advance(TimeSpan.FromSeconds(30));

        model.Start();
        clock.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal((2, StartStage.Starting), (starter.Starts, model.Stage));
    }

    [Fact]
    public void A_start_that_fails_shows_Didnt_start_with_Retry()
    {
        var model = new SindriStartViewModel(new FakeStarter { Failure = "/bin/sh didn't start" }, new FakeTimeProvider());

        model.Begin();

        Assert.Equal((StartStage.DidntStart, "Sindri didn't start: /bin/sh didn't start.", "Retry"), (model.Stage, model.Message, model.Primary));
    }

    [Fact]
    public void With_no_starter_the_page_keeps_its_terminal_command()
    {
        var model = new SindriStartViewModel(starter: null, new FakeTimeProvider());

        model.Begin();

        Assert.Equal((StartStage.Idle, true, false), (model.Stage, model.ShowsTerminalCommand, model.HasPrimary));
    }

    [Fact]
    public void The_Not_running_line_is_hidden_only_while_Didnt_start_shows()
    {
        var clock = new FakeTimeProvider();
        var model = new SindriStartViewModel(new FakeStarter { Error = "SND-CLI-002 exec: x" }, clock);
        var idle = model.HidesNotRunningReason;
        model.Begin();
        var starting = model.HidesNotRunningReason;
        clock.Advance(TimeSpan.FromSeconds(30));
        var didntStart = model.HidesNotRunningReason;
        model.Start();
        var retrying = model.HidesNotRunningReason;

        Assert.Equal((false, false, true, false), (idle, starting, didntStart, retrying));
    }

    [Fact]
    public void A_serve_that_exits_while_starting_shows_Didnt_start_at_once()
    {
        var starter = new FakeStarter { Error = "SND-CLI-002 'sindri serve' exited: SND-API-003 address already in use" };
        var model = new SindriStartViewModel(starter, new FakeTimeProvider());
        model.Begin();

        starter.Exit();

        Assert.Equal((StartStage.DidntStart, "Sindri didn't start: address already in use (exit code 1).", "Retry"), (model.Stage, model.Message, model.Primary));
    }

    [Fact]
    public void A_serve_refused_as_already_running_that_exits_shows_Already_starting_at_once()
    {
        var starter = new FakeStarter { Error = "SND-LEASE-001 serve is already running" };
        var model = new SindriStartViewModel(starter, new FakeTimeProvider());
        model.Begin();

        starter.Exit();

        Assert.Equal((StartStage.AlreadyStarting, false), (model.Stage, model.HasPrimary));
    }

    [Fact]
    public void A_later_outage_shows_the_button_and_starts_nothing()
    {
        var starter = new FakeStarter();
        var model = new SindriStartViewModel(starter, new FakeTimeProvider());
        model.Observe(ShellState.Connecting);
        model.Observe(ShellState.Normal);

        model.Observe(ShellState.NotRunning);

        Assert.Equal((0, StartStage.Idle, "Start Sindri"), (starter.Starts, model.Stage, model.Primary));
    }

    [Fact]
    public void The_first_resolved_state_being_Not_running_starts_Sindri_once()
    {
        var starter = new FakeStarter();
        var model = new SindriStartViewModel(starter, new FakeTimeProvider());
        model.Observe(ShellState.Connecting);

        model.Observe(ShellState.NotRunning);
        model.Observe(ShellState.NotRunning);

        Assert.Equal((1, StartStage.Starting), (starter.Starts, model.Stage));
    }

    [Fact]
    public void Only_a_connected_state_releases_the_start()
    {
        var starter = new FakeStarter();
        var model = new SindriStartViewModel(starter, new FakeTimeProvider());
        model.Observe(ShellState.NotRunning);

        model.Observe(ShellState.Connecting);
        model.Observe(ShellState.Stale);
        model.Observe(ShellState.Unreadable);
        var before = (starter.Releases, model.Stage);
        model.Observe(ShellState.Normal);

        Assert.Equal(((0, StartStage.Starting), 1), (before, starter.Releases));
    }

    [Fact]
    public void An_exit_of_an_older_launch_changes_nothing()
    {
        var clock = new FakeTimeProvider();
        var starter = new FakeStarter { Error = "SND-CLI-002 exec: x" };
        var model = new SindriStartViewModel(starter, clock);
        model.Begin();
        clock.Advance(TimeSpan.FromSeconds(30));
        model.Start();   // Retry: launch 2

        starter.Exit(launch: 1, code: 1);

        Assert.Equal((StartStage.Starting, 2), (model.Stage, starter.Launch));

        starter.Exit(launch: 2, code: 1);

        Assert.Equal(StartStage.DidntStart, model.Stage);
    }

    private sealed class FakeStarter : ISindriStarter
    {
        public event EventHandler<SindriExitedEventArgs>? Exited;

        public int Launch { get; private set; }

        public void Exit(int? launch = null, int code = 1) => Exited?.Invoke(this, new SindriExitedEventArgs(launch ?? Launch, code));

        public int Starts { get; private set; }

        public string? Error { get; init; }

        public string? Failure { get; init; }

        public SindriStartOutcome Outcome { get; init; } = SindriStartOutcome.Started;

        public int Releases { get; private set; }

        public void Release() => Releases++;

        public string LogPath => "/logs/serve.log";

        public string? LastError() => Error;

        public Result<SindriStartOutcome> Start()
        {
            Starts++;
            Launch++;
            return Failure is null ? Outcome : new Error("SND-CLI-002", ErrorKind.Unavailable, Failure);
        }
    }
}
