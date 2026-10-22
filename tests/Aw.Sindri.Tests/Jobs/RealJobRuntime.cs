using System.Globalization;
using System.Text.Json.Serialization.Metadata;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Locks;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.Jobs;
using Aw.Sindri.Host;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Jobs;
using Aw.Sindri.Tests.Commands.Authorization;
using Aw.Sindri.Tests.Commands.Chain;
using Aw.Sindri.Tests.Server;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>
/// The real job runtime over in-memory ports (API P6 unit C): the chained start of the echo and probe jobs (AddJob), the real <see cref="JobRunner"/>
/// behind it, the in-memory ledger and a fixture command for the gate check. Time is the harness's FakeTimeProvider.
/// </summary>
internal sealed class RealJobRuntime : IJobRuntimeSubject
{
    private readonly ServiceProvider _provider;
    private readonly HeavyTakes _heavy = new();

    /// <summary>Wires the runtime; a test adds the catalog rows and services of the job it exercises through <paramref name="rows"/> and <paramref name="configure"/>.</summary>
    /// <param name="scrubber">Cleans what the writer records; a pass-through by default.</param>
    /// <param name="rows">More catalog rows.</param>
    /// <param name="configure">More registrations, made before the provider is built.</param>
    public RealJobRuntime(ITextScrubber? scrubber = null, IEnumerable<CommandRow>? rows = null, Action<IServiceCollection, SliceHarness>? configure = null)
    {
        var options = AwJson.CreateOptions(JsonTypeInfoResolver.Combine(SindriJsonContext.Default, EchoJsonContext.Default));
        CommandRow[] own =
        [
            Rows.Of(ActionClass.Job, "EchoJob", typeof(EchoJobRequest)),
            Rows.Of(ActionClass.Job, "ProbeJob", typeof(ProbeJobRequest)),
            Rows.Of(ActionClass.Desk, "FixtureCommand", typeof(FixtureCommand)),
        ];
        var catalog = new InMemoryCommandCatalog([.. own, .. rows ?? []]);
        var services = new ServiceCollection();
        configure?.Invoke(services, H);
        AddProbeJob(services);
        _provider = services
            .AddSingleton<TimeProvider>(H.Clock)
            .AddSingleton<Kernel.Ids.IIdGenerator>(H.Ids)
            .AddSingleton<ILedgerStore>(H.Store)
            .AddSingleton(H.Held)
            .AddSingleton(H.Events)
            .AddSingleton(H.Callers)
            .AddSingleton(options)
            .AddSingleton<ICommandCatalog>(catalog)
            .AddSingleton<IProjectorStatus>(new ProjectorProgress(H.Clock))
            .AddSingleton<IHeavySlots>(_heavy)
            .AddSingleton(scrubber ?? new PassThroughScrubber())
            .AddSingleton<IJobContextAccessor>(new FakeJobContextAccessor())
            .AddSingleton<EchoJobProbe>()
            .AddSingleton(Latch)
            .AddSingleton<IShutdown>(Shutdown)
            .AddSingleton<Microsoft.Extensions.Logging.ILogger<AuditTrail>>(new ListLogger<AuditTrail>())
            .AddSingleton<JobRegistry>()
            .AddSingleton(static sp => new JobStartRecorder(sp.GetRequiredService<HeldLease>(), sp.GetRequiredService<ILedgerStore>(), sp.GetRequiredService<EventFactory>()))
            .AddSingleton(static sp => new JobEventWriter(sp.GetRequiredService<ILedgerStore>(), sp.GetRequiredService<HeldLease>(), sp.GetRequiredService<EventFactory>(),
                sp.GetRequiredService<ITextScrubber>(), sp.GetRequiredService<ICallerContextAccessor>()))
            .AddSingleton(static sp => new JobRunner(() => { var scope = sp.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope(); return new JobScope(scope.ServiceProvider.GetRequiredService, scope); }, sp.GetRequiredService<IJobContextAccessor>(),
                sp.GetRequiredService<ICallerContextAccessor>(), sp.GetRequiredService<JobEventWriter>(), sp.GetRequiredService<JobRegistry>(), sp.GetRequiredService<IShutdown>(), sp.GetRequiredService<JobWaits>()))
            .AddSingleton<JobWaits>().AddSingleton<IJobSignals>(static sp => sp.GetRequiredService<JobWaits>())
            .AddSingleton<IJobRunner>(static sp => sp.GetRequiredService<JobRunner>()).AddSingleton<IJobCancellation>(static sp => sp.GetRequiredService<JobRunner>())
            .AddJob<EchoJobRequest, EchoReport, EchoJobService>(static _ => JobKey.From("echo"), static r => string.Create(CultureInfo.InvariantCulture, $"echoed {r.Lines}"))
            .AddScoped<IValidator<FixtureCommand>, FixtureValidator>().AddCommand<FixtureCommand, CommandAccepted, FixtureService>()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private static void AddProbeJob(IServiceCollection services) =>
        services.AddJob<ProbeJobRequest, ProbeReport, ProbeJobService>(static r => JobKey.From(r.Mode), static r => string.Create(CultureInfo.InvariantCulture, $"probed {r.Text.Length}"),
            precheck: static (r, _) => ValueTask.FromResult<Result<Unit>>(string.Equals(r.Mode, "refused", StringComparison.Ordinal) ? SindriErrors.ServiceNotFound.With("the precheck said no") : Unit.Value),
            repo: static r => RepoOf(r.Mode));

    private static RepoName? RepoOf(string mode) => mode switch
    {
        "repo" => RepoName.From("demo"),
        "repo-throws" => throw new InvalidOperationException("the repo lambda broke"),
        _ => null,
    };

    public SliceHarness H { get; } = new();

    /// <summary>The process shutdown the runner watches; a test trips it with Request.</summary>
    public FakeShutdown Shutdown { get; } = new();

    public FixtureLatch Latch { get; } = new();

    /// <summary>Resolves a singleton the runtime was built with.</summary>
    /// <typeparam name="T">The service.</typeparam>
    /// <returns>The service.</returns>
    public T Get<T>()
        where T : notnull => _provider.GetRequiredService<T>();

    public JobRunner Runner => _provider.GetRequiredService<JobRunner>();

    public EchoJobProbe Probe => _provider.GetRequiredService<EchoJobProbe>();

    public IReadOnlyList<LedgerEvent> Events => H.Store.Events;

    public int HeavySlotsHeld => _heavy.Takes;

    /// <summary>Takes the lease the runtime writes under.</summary>
    /// <returns>A task that completes then.</returns>
    public async Task Open() => _ = await H.HoldLease();

    public Task<Result<JobStartedResponse>> Start(EchoJobRequest request, CallerContext caller) => Start<EchoJobRequest>(request, caller);

    /// <summary>Starts any registered job request through its chain as <paramref name="caller"/>.</summary>
    /// <typeparam name="TRequest">The job request.</typeparam>
    /// <param name="request">The request.</param>
    /// <param name="caller">The starting caller.</param>
    /// <returns>The answer.</returns>
    public async Task<Result<JobStartedResponse>> Start<TRequest>(TRequest request, CallerContext caller)
        where TRequest : notnull
    {
        using var entered = H.Callers.Enter(caller);
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IService<TRequest, JobStartedResponse>>().Handle(request, TestContext.Current.CancellationToken);
    }

    /// <summary>Starts an echo job as <paramref name="caller"/> with a request token of the test's choosing (an aborted POST).</summary>
    /// <param name="caller">The starting caller.</param>
    /// <param name="cancellationToken">The request's token.</param>
    /// <returns>The answer.</returns>
    public async Task<Result<JobStartedResponse>> StartWith(CallerContext caller, CancellationToken cancellationToken)
    {
        using var entered = H.Callers.Enter(caller);
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IService<EchoJobRequest, JobStartedResponse>>().Handle(new EchoJobRequest(Lines: 1, DelayMs: 0), cancellationToken);
    }

    /// <summary>Runs any registered command through its chain as <paramref name="caller"/>.</summary>
    /// <typeparam name="TRequest">The request.</typeparam>
    /// <typeparam name="TResponse">The response.</typeparam>
    /// <param name="request">The request.</param>
    /// <param name="caller">The caller.</param>
    /// <returns>The answer.</returns>
    public async Task<Result<TResponse>> Run<TRequest, TResponse>(TRequest request, CallerContext caller)
        where TRequest : notnull
    {
        using var entered = H.Callers.Enter(caller);
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IService<TRequest, TResponse>>().Handle(request, TestContext.Current.CancellationToken);
    }

    /// <summary>Runs a registered command through its chain as <paramref name="caller"/> in a scope the test gets to prepare first (the Onyx login's per-request hand-over lives in one).</summary>
    /// <typeparam name="TRequest">The request.</typeparam>
    /// <typeparam name="TResponse">The response.</typeparam>
    /// <param name="request">The request.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="prepare">Called with the scope's provider before the command runs.</param>
    /// <returns>The answer.</returns>
    public async Task<Result<TResponse>> RunPrepared<TRequest, TResponse>(TRequest request, CallerContext caller, Action<IServiceProvider> prepare)
        where TRequest : notnull
    {
        ArgumentNullException.ThrowIfNull(prepare);
        using var entered = H.Callers.Enter(caller);
        await using var scope = _provider.CreateAsyncScope();
        prepare(scope.ServiceProvider);
        return await scope.ServiceProvider.GetRequiredService<IService<TRequest, TResponse>>().Handle(request, TestContext.Current.CancellationToken);
    }

    public async Task<Result<CommandAccepted>> Cancel(JobId job)
    {
        var caller = A.Caller().Human().Build();
        using var entered = H.Callers.Enter(caller);
        Runner.Cancelling(job);
        var entry = H.Events.Entry(EventTypes.JobCancelled, new JobCancelled(caller.CommandId, job, caller.Actor), $"{EventTypes.JobCancelled}:{job.Value}");
        var recorded = false;
        try
        {
            var appended = await H.Store.Append(entry, H.Held.Current!, CancellationToken.None);
            if (appended.IsError)
            {
                return appended.Error;
            }

            _ = Runner.Cancel(job);
            recorded = true;
            return new CommandAccepted(caller.CommandId, appended.Value.Sequence);
        }
        finally
        {
            if (!recorded)
            {
                Runner.Withdraw(job);   // as CancelJob does: the job writes its own end
            }
        }
    }

    public async Task Quiesce()
    {
        try
        {
            await Runner.Idle().WaitAsync(JobHost.Hang, TimeProvider.System, TestContext.Current.CancellationToken);   // a hang guard on the real clock: the harness clock is fake and would never fire
        }
        catch (TimeoutException e)
        {
            throw new TimeoutException($"jobs still running {JobHost.Hang.TotalSeconds.ToString("0", CultureInfo.InvariantCulture)} s after the test asked them to end: {string.Join(", ", Runner.Active)}", e);
        }
    }

    public async Task<bool> CommandGateIsFree()
    {
        var caller = A.Caller().Human().Build();
        using var entered = H.Callers.Enter(caller);
        var run = Task.Run(async () =>
        {
            await using var scope = _provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IService<FixtureCommand, CommandAccepted>>().Handle(new FixtureCommand("accept"), TestContext.Current.CancellationToken);
        });
        try
        {
            return (await run.WaitAsync(JobHost.Hang, TimeProvider.System, TestContext.Current.CancellationToken)).IsSuccess;   // a hang guard on the real clock, not a wait: the harness clock is fake and would never fire
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        Shutdown.Dispose();
    }

    private sealed class HeavyTakes : IHeavySlots
    {
        private int _takes;

        public int Takes => Volatile.Read(ref _takes);

        public ValueTask<Result<HeavySlotTaken>> Take(string holder, TimeSpan timeout, IProgress<string>? waiting, CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref _takes);
            throw new NotSupportedException("the job runtime must not take a heavy slot");
        }
    }
}
