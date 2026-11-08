using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Commands;

/// <summary>What the 'status --follow' tests share: the verb over a <see cref="FollowApi"/>, its output, an interrupt and a counting fake clock.</summary>
internal sealed class FollowRig : IDisposable
{
    private static readonly FakeControlPlaneProbe _probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private static readonly ProfileHash _hash = ProfileHash.From("3f1c2a" + new string('0', 58));
    private readonly CancellationTokenSource _interrupt = new();

    public StringWriter Out { get; } = new();

    public StringWriter Error { get; } = new();

    public CountingClock Clock { get; } = new();

    public void Dispose()
    {
        _interrupt.Dispose();
        Out.Dispose();
        Error.Dispose();
    }

    public static string Status(bool needsApproval) =>
        Encoding.UTF8.GetString(AwJson.Serialize(new GetStatusResponse(Running: true, 42, 3, LeaseExpiresAt: null,
            new ProfileStatus(_hash, Problem: needsApproval ? "p" : null, "/p.yaml"), []), SliceHarness.Json));

    public Task<int> Start(FollowApi api, bool json)
    {
        var client = new ControlPlaneClient(_probe, SliceHarness.Json,
            CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), InMemoryHumanTokenStore.WithToken(), _ => ValueTask.FromResult((RepoName?)null)), handler: api);
        var verbs = new FollowVerbs(client, _probe, new Output(Out, Error, SliceHarness.Json), Clock);
        return Task.Run(() => verbs.Run(json, _interrupt.Token));
    }

    public Task Interrupt() => _interrupt.CancelAsync();

    public static Task<int> Finished(Task<int> run) => run.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);

    public static async Task WaitUntil(Func<bool> condition)
    {
        var until = RealTime.NowDateTime.AddSeconds(15);
        while (!condition())
        {
            Assert.True(RealTime.NowDateTime < until, "timed out waiting");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Moves the fake clock by <paramref name="step"/> until <paramref name="done"/> holds: a timer the verb sets after this call still fires on a later step.</summary>
    public async Task Tick(TimeSpan step, Func<bool> done)
    {
        for (var i = 0; i < 1_000 && !done(); i++)
        {
            Clock.Advance(step);
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(done(), "the condition never held while the clock moved");
    }

    /// <summary>A fake clock that counts the timers set on it, so a test can wait for the verb to be waiting.</summary>
    internal sealed class CountingClock : FakeTimeProvider
    {
        private readonly List<(int Count, TaskCompletionSource Reached)> _waiting = [];
        private int _timers;

        public CountingClock()
            : base(KnownInstants.Epoch)
        {
        }

        public int Timers => Volatile.Read(ref _timers);

        /// <summary>A task that completes once at least <paramref name="count"/> timers have been set on this clock (no wall-clock wait).</summary>
        /// <param name="count">How many timers.</param>
        /// <returns>The task.</returns>
        public Task TimersReached(int count)
        {
            lock (_waiting)
            {
                if (Timers >= count)
                {
                    return Task.CompletedTask;
                }

                var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _waiting.Add((count, reached));
                return reached.Task;
            }
        }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = base.CreateTimer(callback, state, dueTime, period);
            lock (_waiting)
            {
                var now = Interlocked.Increment(ref _timers);
                foreach (var (count, reached) in _waiting.Where(w => w.Count <= now).ToList())
                {
                    _ = reached.TrySetResult();
                    _ = _waiting.Remove((count, reached));
                }
            }

            return timer;
        }
    }
}
