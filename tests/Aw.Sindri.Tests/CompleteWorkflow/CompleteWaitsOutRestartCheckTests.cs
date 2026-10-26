using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.CompleteStep;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Host.Endpoints;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Commands;
using Aw.Sindri.Tests.Layout;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.CompleteWorkflow;

/// <summary>
/// The #1109 check (#1000 ask 1): a launched agent's 'workflow complete' that finds the control plane down (SND-API-001) keeps trying until it is back, for a bounded time that covers a reinstall
/// restart, and then is accepted; when the window ends it says where the output stays. A human's call is not held. The clock is a FakeTimeProvider that moves only once the command says it is
/// waiting, so no wait is real and the window starts on the clock the test started from (the first copy of this check moved it on a fixed loop that raced the command's own file read).
/// </summary>
[Lane(TestLane.Unit)]
public sealed class CompleteWaitsOutRestartCheckTests : IDisposable
{
    private static readonly FakeControlPlaneProbe _probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private static readonly TimeSpan _restart = TimeSpan.FromMinutes(5);   // a reinstall's restart: stop, publish, start
    private static readonly TimeSpan _anyWindow = TimeSpan.FromMinutes(30);   // the most a bounded wait may take
    private static readonly TimeSpan _realBound = TimeSpan.FromSeconds(30);   // the most the test itself may take
    private readonly FakeTimeProvider _clock = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly TextWriter _errorShared;   // the command writes from another thread while Waiting reads
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "aw-1109-" + Path.GetRandomFileName(), ".sindri-out");
    private DateTimeOffset _upAt;
    private int _tries;

    public CompleteWaitsOutRestartCheckTests()
    {
        _errorShared = TextWriter.Synchronized(_error);
        _ = Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "output"), "the result");
    }

    public void Dispose()
    {
        _out.Dispose();
        _errorShared.Dispose();
        _error.Dispose();
        Directory.Delete(Path.GetDirectoryName(_folder)!, recursive: true);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string[] Args() => ["ITM-1/step/1", "--run", "1.1", "--outcome", "done", "--output", "fact:output=" + Path.Combine(_folder, "output"), "--nonce", new string('a', 32)];

    private static RoutedApi Api()
    {
        var instance = InstanceSummaries.Of(DevInstances.All[0].View, new InstanceSummaryContext(DevInstances.AsOf));
        var answered = new CompleteStepResponse(new CommandResult(DevInstances.All[0].View, "-> checks"), CommandNonce.From(new string('a', 32)), AlreadyApplied: false);
        return new RoutedApi()
            .On("/sindri/workflow/instances", HttpStatusCode.OK, new InstanceList([instance], new ListCounts(1, 0, 0, 0, 0), Empty: null, DevInstances.AsOf))
            .OnPost($"/sindri/workflow/instances/{instance.Id.Value}/runs/{InstanceRoute.Segment("1.1")}/complete", HttpStatusCode.OK, answered);
    }

    // The start rule of a session: it never starts the control plane, it only finds it down until the fake time says it is back.
    private Task<Kernel.Error?> Ensure(CancellationToken cancellationToken)
    {
        _tries++;
        return Task.FromResult(_clock.GetUtcNow() < _upAt ? SindriErrors.NotRunning.With("the control plane went away") : null);
    }

    /// <summary>Runs 'workflow complete' with the control plane down until <see cref="_upAt"/> (fake time), see <see cref="Drive"/>.</summary>
    private async Task<int> Complete(RoutedApi api, bool launched, TimeSpan downFor)
    {
        var started = _clock.GetUtcNow();
        _upAt = started + downFor;
        var client = new ControlPlaneClient(_probe, SliceHarness.Json,
            CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), InMemoryHumanTokenStore.WithToken(), _ => ValueTask.FromResult((RepoName?)null)), handler: api);
        var desk = new RecipeDesk(client, TextReader.Null, Ensure,
            launched ? static name => name == "AW_SINDRI_LAUNCH_SECRET" ? "a-secret" : null : null);
        await using var services = new ServiceCollection().AddSingleton<TimeProvider>(_clock).BuildServiceProvider();
        var run = Host.Commands.CompleteCommand.Run(Args(), desk, services, new Output(_out, _errorShared, SliceHarness.Json), Token);
        await Drive(run, started);
        return await run.WaitAsync(_realBound, TimeProvider.System, Token);   // why: a real bound; the fake clock stops moving once the drive ends
    }

    /// <summary>
    /// Moves the fake clock only while the command waits: not at all until its first "retrying in" line, then a second at a time with a short real pause between steps, so each of the command's
    /// own timers is set before the clock passes it. Stops when the command ends, after <see cref="_anyWindow"/> of fake time, or after <see cref="_realBound"/> of real time.
    /// </summary>
    private async Task Drive(Task<int> run, DateTimeOffset started)
    {
        var real = TimeProvider.System.GetTimestamp();
        while (!run.IsCompleted && _clock.GetUtcNow() - started < _anyWindow && TimeProvider.System.GetElapsedTime(real) < _realBound)
        {
            if (Waiting())
            {
                _clock.Advance(TimeSpan.FromSeconds(1));
            }

            await Task.Delay(TimeSpan.FromMilliseconds(2), TimeProvider.System, Token);
        }
    }

    private bool Waiting()
    {
        lock (_errorShared)   // why: the synchronized writer locks on itself
        {
            return _error.ToString().Contains("retrying in", StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task A_launched_sessions_complete_waits_out_a_restart_and_is_accepted()
    {
        var api = Api();

        var code = await Complete(api, launched: true, downFor: _restart);

        Assert.Equal(0, code);
        Assert.Contains(api.Calls, static c => c.Method == "POST" && c.Path.EndsWith("/complete", StringComparison.Ordinal));
        Assert.True(_tries > 1);
        Assert.Contains("SND-API-001", _error.ToString(), StringComparison.Ordinal);   // each wait says why
    }

    [Fact]
    public async Task A_control_plane_that_stays_down_ends_the_wait_in_exit_1_inside_the_bound_and_says_where_the_output_stays()
    {
        var started = _clock.GetUtcNow();

        var code = await Complete(Api(), launched: true, downFor: TimeSpan.FromDays(1));

        Assert.Equal(1, code);
        var waited = _clock.GetUtcNow() - started;
        Assert.InRange(waited, _restart, _anyWindow - TimeSpan.FromSeconds(1));
        Assert.Contains(_folder, _error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_humans_complete_is_not_held_and_says_not_running_at_once()
    {
        var code = await Complete(Api(), launched: false, downFor: TimeSpan.FromDays(1));

        Assert.Equal(1, code);
        Assert.Equal(1, _tries);
        Assert.Contains("SND-API-001", _error.ToString(), StringComparison.Ordinal);
    }
}
