using Aw.Kernel.Ids;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features;
using Aw.Sindri.Host.Server;
using Aw.Sindri.Infrastructure.Views;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Server;

/// <summary>A scheduler on the stepped fake clock with the real pause view and guard (API P6 unit D): a test pauses, resumes and steps seconds, and reads what was recorded.</summary>
internal sealed class SchedulerRig : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly SteppedClock _clock;
    private readonly IReadOnlyList<BuiltInService> _builtIns;
    private SchedulerHost _scheduler;
    private readonly InMemoryViewStore _views = new(SliceHarness.Json);
    private long _sequence;

    private SchedulerRig(IEnumerable<IScheduledService> services, IEnumerable<BuiltInService> builtIns)
    {
        Reader = new SwitchableLedgerReader(H.Store);
        var collection = new ServiceCollection()
            .AddSingleton<ILedgerStore>(H.Store)
            .AddSingleton<ILedgerReader>(Reader)
            .AddSingleton(H.Events)
            .AddSingleton<TimeProvider>(H.Clock)
            .AddSingleton(H.Callers)
            .AddSingleton<IIdGenerator>(H.Ids)
            .AddSingleton<ITextScrubber>(new PassThroughScrubber())
            .AddRecordServiceRun();
        foreach (var service in services)
        {
            _ = collection.AddSingleton(service);
        }

        Progress = new ProjectorProgress(H.Clock);
        _provider = collection.BuildServiceProvider();
        _clock = new SteppedClock(H.Clock);
        _builtIns = [.. builtIns];
        _scheduler = NewScheduler();
    }

    public SliceHarness H { get; } = new();

    /// <summary>The scheduler's ledger reader; a test can make it fail.</summary>
    public SwitchableLedgerReader Reader { get; }

    /// <summary>What the scheduler logged.</summary>
    public ListLogger<SchedulerHost> Log { get; } = new();

    /// <summary>The projector status the pause view reads.</summary>
    internal ProjectorProgress Progress { get; }

    /// <summary>The next slots the scheduler publishes for the services listing.</summary>
    public LiveSchedule Live { get; } = new();

    public ServiceRunGuard Guard { get; private set; } = new();

    /// <summary>Approves a profile naming <paramref name="every"/> for each service, takes the lease and leaves the scheduler to <see cref="Begin"/>.</summary>
    /// <param name="services">The services this build has.</param>
    /// <param name="every">The services the profile names, each with its cadence.</param>
    /// <returns>The running rig.</returns>
    public static Task<SchedulerRig> Start(IEnumerable<IScheduledService> services, params (string Name, string Every)[] every) => Start(services, [], every);

    /// <summary>As the overload without built-ins, with built-in services the profile does not name.</summary>
    /// <param name="services">The services this build has.</param>
    /// <param name="builtIns">The built-in services.</param>
    /// <param name="every">The services the profile names, each with its cadence.</param>
    /// <returns>The running rig.</returns>
    public static async Task<SchedulerRig> Start(IEnumerable<IScheduledService> services, IEnumerable<BuiltInService> builtIns, params (string Name, string Every)[] every)
    {
        var rig = new SchedulerRig(services, builtIns);
        _ = await rig.H.Profiles.Save(SliceHarness.ProfileOf("thor", every), TestContext.Current.CancellationToken);
        _ = await rig.H.HoldLease();
        return rig;
    }

    /// <summary>Starts the scheduler (its first slot is due at once).</summary>
    /// <returns>A task that completes then.</returns>
    public Task Begin() => _scheduler.StartAsync(TestContext.Current.CancellationToken);

    /// <summary>Advances the fake clock <paramref name="seconds"/> one second at a time, letting the loop finish each step.</summary>
    /// <param name="seconds">How many.</param>
    /// <returns>A task that completes then.</returns>
    public async Task Step(int seconds)
    {
        for (var n = 0; n < seconds; n++)
        {
            await _clock.Step(TimeSpan.FromSeconds(1));
            await _clock.Armed();
        }
    }

    /// <summary>Stops the process's scheduler and starts a new one on the same ledger, as a restart does (the run guard is in memory, so it starts empty).</summary>
    /// <param name="down">How long the process stays down before the new one starts.</param>
    /// <returns>A task that completes when the new scheduler runs.</returns>
    public async Task Restart(TimeSpan down = default)
    {
        await _scheduler.StopAsync(TestContext.Current.CancellationToken);
        _scheduler.Dispose();
        H.Clock.Advance(down);
        Guard = new ServiceRunGuard();
        _scheduler = NewScheduler();
        await Begin();
    }

    /// <summary>Approves a profile naming <paramref name="every"/> for each service; the scheduler picks it up on its next tick.</summary>
    /// <param name="every">The services the profile names, each with its cadence.</param>
    /// <returns>A task that completes then.</returns>
    public async Task Reapprove(params (string Name, string Every)[] every) =>
        _ = await H.Profiles.Save(SliceHarness.ProfileOf("thor", every), TestContext.Current.CancellationToken);

    public Task Pause(string service) => Fold(EventTypes.ServicePaused, new ServicePaused(H.Ids.New<CommandId>(), ServiceName.From(service)));

    /// <summary>The views start a rebuild: the status says so and the services view is emptied, to be refolded.</summary>
    /// <returns>A task that completes then.</returns>
    public async Task BeginRebuild()
    {
        Progress.Publish(Progress.Current with { Rebuilding = true });
        _ = await _views.Reset([ViewNames.Services], ViewSchema.Version, H.Clock.GetUtcNow(), TestContext.Current.CancellationToken);
        _sequence = 0;
    }

    /// <summary>The rebuild is done: the pauses given are refolded and the status is fresh again.</summary>
    /// <param name="paused">The services paused in the ledger.</param>
    /// <returns>A task that completes then.</returns>
    public async Task EndRebuild(params string[] paused)
    {
        foreach (var service in paused)
        {
            await Pause(service);
        }

        Progress.Publish(Progress.Current with { Rebuilding = false });
    }

    public Task Resume(string service) => Fold(EventTypes.ServiceResumed, new ServiceResumed(H.Ids.New<CommandId>(), ServiceName.From(service)));

    /// <summary>The recorded runs of <paramref name="service"/>, in order.</summary>
    /// <param name="service">The service.</param>
    /// <returns>The runs.</returns>
    public IReadOnlyList<ServiceRan> Runs(string service) => [.. H.Store.Events
        .Where(e => string.Equals(e.Envelope.Type, EventTypes.ServiceRan, StringComparison.Ordinal))
        .Select(e => H.Events.Payload<ServiceRan>(e).Value)
        .Where(r => string.Equals(r.Service.Value, service, StringComparison.Ordinal))];

    public async ValueTask DisposeAsync()
    {
        await _scheduler.StopAsync(TestContext.Current.CancellationToken);
        _scheduler.Dispose();
        await _provider.DisposeAsync();
    }

    private SchedulerHost NewScheduler() => new(_provider.GetRequiredService<IServiceScopeFactory>(), new ScheduleInputs(H.Profiles, _builtIns, new ViewServicePauses(_views, Progress), Live), Guard, H.Held, H.Callers, _clock, Log);

    private async Task Fold<TPayload>(string type, TPayload payload)
    {
        var ct = TestContext.Current.CancellationToken;
        _ = (await _views.Open(ViewSchema.Version, ct)).Value;
        var applied = (await _views.Apply([Recorded.Of(H.Events, type, payload, ++_sequence)], [new ServicesProjection(H.Events)], H.Clock.GetUtcNow(), ct)).Value;
        Assert.Null(applied.Stall);
    }
}
