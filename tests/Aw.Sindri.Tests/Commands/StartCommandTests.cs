using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Server;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Commands;

/// <summary>Finding or starting the control plane, over a scripted probe and launcher, on the fake clock.</summary>
public sealed class StartCommandTests : IDisposable
{
    private static readonly (ControlEndpoint, Ping) _found = (new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private readonly ScriptedProbe _probe = new();
    private readonly SteppedClock _clock = new(new FakeTimeProvider(KnownInstants.Epoch));
    private readonly ControlPlaneClient _client;

    public StartCommandTests()
    {
        _client = new ControlPlaneClient(_probe, SliceHarness.Json);
    }

    public void Dispose() => _client.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task A_control_plane_that_answers_late_is_reported_and_nothing_is_launched()
    {
        var launcher = new FakeControlPlaneLauncher();
        _probe.Answers.Enqueue(SindriErrors.ApiTimeout.With("127.0.0.1:7410 didn't answer the ping within 2 s"));

        var found = await Command(launcher).FindOrLaunch(Token);

        Assert.Equal(SindriErrors.ApiTimeout.Code, found.Error.Code);
        Assert.Equal(0, launcher.Launches);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_concurrent_start_that_won_the_lease_is_found_by_polling_on()
    {
        var launcher = FakeControlPlaneLauncher.ExitingWith("SND-LEASE-001 held by host/77 since then (epoch 3)");
        _probe.Answers.Enqueue(SindriErrors.NotRunning.With("no control plane is recorded"));
        _probe.Answers.Enqueue(SindriErrors.NotRunning.With("not yet"));
        _probe.Answers.Enqueue(SindriErrors.NotRunning.With("not yet"));
        _probe.Answers.Enqueue(_found);

        var found = await Drive(Command(launcher).FindOrLaunch(Token));

        Assert.Equal(_found, found.Value);
        Assert.Equal(1, launcher.Launches);
        Assert.Equal(4, _probe.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lost_lease_race_whose_winner_never_answers_fails_at_the_deadline()
    {
        var launcher = FakeControlPlaneLauncher.ExitingWith("SND-LEASE-001 held by host/77 since then (epoch 3)");

        var found = await Drive(Command(launcher).FindOrLaunch(Token));

        Assert.Equal(SindriErrors.StartFailed.Code, found.Error.Code);
        Assert.Contains("didn't answer within 10 s: the started one exited with SND-LEASE-001 held by host/77 since then (epoch 3)", found.Error.Message, StringComparison.Ordinal);
        Assert.Equal(1 + (int)(StartCommand.Budget / StartCommand.Poll), _probe.Calls);   // the first check, then every poll to the deadline
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_child_that_exits_with_another_error_fails_at_once_with_its_line()
    {
        var launcher = FakeControlPlaneLauncher.ExitingWith("SND-API-003 address already in use");

        var found = await Drive(Command(launcher).FindOrLaunch(Token));

        Assert.Equal(SindriErrors.StartFailed.Code, found.Error.Code);
        Assert.Contains("SND-API-003 address already in use", found.Error.Message, StringComparison.Ordinal);
        Assert.Equal(2, _probe.Calls);   // the first check and one poll: it fails at once, not at the deadline
    }

    private StartCommand Command(FakeControlPlaneLauncher launcher) =>
        new(_probe, launcher, _client, new Output(TextWriter.Null, TextWriter.Null, SliceHarness.Json), _clock);

    /// <summary>Steps the fake clock one poll at a time, each once the start wait armed its delay, until <paramref name="run"/> ends.</summary>
    private async Task<T> Drive<T>(Task<T> run)
    {
        while (true)
        {
            await _clock.Armed(() => run.IsCompleted);
            if (run.IsCompleted)
            {
                return await run;
            }

            await _clock.Step(StartCommand.Poll);
        }
    }

    /// <summary>A probe answering from a queue, then 'not running' once it runs out.</summary>
    private sealed class ScriptedProbe : IControlPlaneProbe
    {
        private int _calls;

        public Queue<Result<(ControlEndpoint Endpoint, Ping Ping)>> Answers { get; } = new();

        public int Calls => Volatile.Read(ref _calls);

        public ValueTask<Result<(ControlEndpoint Endpoint, Ping Ping)>> Check(CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref _calls);
            return ValueTask.FromResult(Answers.TryDequeue(out var answer) ? answer : SindriErrors.NotRunning.With("nothing answers"));
        }
    }
}
