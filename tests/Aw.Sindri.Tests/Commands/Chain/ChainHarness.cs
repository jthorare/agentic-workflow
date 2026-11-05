using Aw.Context.Core.Facade;
using Aw.Context.Testing.Engine;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.StopProcess;
using Aw.Sindri.Host.Catalog;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Tests.Commands.Chain;

/// <summary>
/// The real command chains (AddCommand, and AddStopCommand for the stop) over in-memory ports: StopProcess, ApproveProfile and the fixture command, with
/// the shell's catalog rows plus the fixture's desk row (reading views, or granted to services, when asked).
/// </summary>
internal sealed class ChainHarness : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    public ChainHarness(Func<ILedgerStore, ILedgerStore>? ledger = null, IEndpointFile? endpoints = null, bool fixtureReadsViews = false,
        IReadOnlyList<string>? grants = null, ProverLabel? prover = null)
    {
        FixtureGrants = grants ?? [];
        Ledger = ledger?.Invoke(H.Store) ?? H.Store;
        Progress = new ProjectorProgress(H.Clock);
        var fixture = Authorization.Rows.Of(ActionClass.Desk, "FixtureCommand", typeof(FixtureCommand)) with { ReadsViews = fixtureReadsViews, ServiceGrants = FixtureGrants };
        _provider = new ServiceCollection()
            .AddSingleton<TimeProvider>(H.Clock)
            .AddSingleton<IIdGenerator>(H.Ids)
            .AddSingleton(Ledger)
            .AddSingleton<ILease>(H.Store)
            .AddSingleton(H.Held)
            .AddSingleton(H.Events)
            .AddSingleton(H.Callers)
            .AddSingleton<IShutdown>(H.Shutdown)
            .AddSingleton(new TemporalRuntime(new FakeTemporalServer(), H.Clock))   // P4a: stop stops the worker and the dev server
            .AddSingleton(endpoints ?? new InMemoryEndpointFile())
            .AddSingleton(new HeldHumanToken())
            .AddSingleton(prover ?? new ProverLabel(Allowed: false))
            .AddSingleton<IHumanTokenStore>(new InMemoryHumanTokenStore())
            .AddScoped<ServeRecords>()
            .AddSingleton(new TemporalRuntime(new FakeTemporalServer(), H.Clock))   // the stop's Temporal side, with no server running
            .AddSingleton<IProfileSource>(H.Profiles)
            .AddSingleton<IApprovals>(H.Profiles)
            .AddSingleton<IChallenges>(H.Challenges)
            .AddSingleton<IContextEngine>(FakeContextEngine.Instance)
            .AddSingleton<IAgentCatalog>(new FixedAgentCatalog())
            .AddScoped<ProfileChecks>()
            .AddSingleton<ICommandCatalog>(new InMemoryCommandCatalog([.. ShellRows.All, fixture]))
            .AddSingleton(Latch)
            .AddSingleton<IProjectorStatus>(Progress)
            .AddSingleton<ILogger<AuditTrail>>(Log)
            .AddScoped<IValidator<FixtureCommand>, FixtureValidator>().AddCommand<FixtureCommand, CommandAccepted, FixtureService>()
            .AddScoped<IValidator<StopProcessRequest>, AcceptAllValidator<StopProcessRequest>>().AddStopCommand<StopProcessRequest, StopProcessResponse, StopProcessService>()
            .AddScoped<IValidator<ApproveProfileRequest>, AcceptAllValidator<ApproveProfileRequest>>().AddCommand<ApproveProfileRequest, ApproveProfileResponse, ApproveProfileService>()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    public SliceHarness H { get; } = new();

    /// <summary>The services the fixture's desk row grants (none unless a test asks).</summary>
    public IReadOnlyList<string> FixtureGrants { get; }

    public ILedgerStore Ledger { get; }

    /// <summary>The projector's progress the chain waits on (nothing publishes it unless a test does).</summary>
    public ProjectorProgress Progress { get; }

    public FixtureLatch Latch { get; } = new();

    public ListLogger<AuditTrail> Log { get; } = new();

    /// <summary>The events recorded so far, oldest first.</summary>
    public IReadOnlyList<LedgerEvent> Events => H.Store.Events;

    /// <summary>The audit rows recorded so far.</summary>
    public IReadOnlyList<CommandHandled> Handled =>
        [.. Events.Where(e => e.Envelope.Type == EventTypes.CommandHandled).Select(e => H.Events.Payload<CommandHandled>(e).Value)];

    /// <summary>Runs <paramref name="request"/> through its registered chain as <paramref name="caller"/> (none when null).</summary>
    public async Task<Result<TResponse>> Run<TRequest, TResponse>(TRequest request, CallerContext? caller)
        where TRequest : notnull
    {
        using var entered = caller is null ? null : H.Callers.Enter(caller);
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IService<TRequest, TResponse>>().Handle(request, TestContext.Current.CancellationToken);
    }

    /// <summary>Starts <paramref name="request"/> without waiting for it (for the gate tests); the scope lives until it ends.</summary>
    public Task<Result<CommandAccepted>> Start(FixtureCommand request, CallerContext caller)
    {
        var entered = H.Callers.Enter(caller);
        var running = Run(this, request);   // its first part runs now, inside the caller; the rest keeps the caller it captured
        entered.Dispose();
        return running;

        static async Task<Result<CommandAccepted>> Run(ChainHarness harness, FixtureCommand request)
        {
            await using var scope = harness._provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IService<FixtureCommand, CommandAccepted>>().Handle(request, CancellationToken.None);
        }
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
