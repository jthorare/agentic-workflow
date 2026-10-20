using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The work of a build job (context spec §8.7; API P6): builds the draft through the engine under the starting caller, which is the same work as the
/// 'context-rebuild' service, passes the engine's step lines on as the job's progress and records 'context.map.built' (slot: when the job began).
/// The engine takes the box-wide heavy-job lock (SND-CONTEXT-040 when it stays busy).
/// </summary>
/// <param name="scopes">The profile's scope and the caller.</param>
/// <param name="engine">The context engine.</param>
/// <param name="held">The lease the event is fenced by.</param>
/// <param name="ledger">Appends the event.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="jobs">The running job: its progress takes the engine's lines.</param>
/// <param name="clock">Dates the slot.</param>
internal sealed class BuildContextMapService(ContextScopes scopes, IContextEngine engine, HeldLease held, ILedgerStore ledger, EventFactory events, IJobContextAccessor jobs, TimeProvider clock)
    : IService<BuildContextMapRequest, BuildContextMapResult>
{
    private const int MaxDegraded = 20;

    public async ValueTask<Result<BuildContextMapResult>> Handle(BuildContextMapRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        var slot = clock.GetUtcNow();
        var built = await engine.BuildMap(jobs.Current?.Progress, scope.Value, cancellationToken).ConfigureAwait(false);
        if (built.IsError)
        {
            return built.Error;
        }

        var report = built.Value;
        var recorded = await new ContextMapEvents(ledger, events, grant).Built(report, slot, cancellationToken).ConfigureAwait(false);
        return recorded.IsError
            ? recorded.Error
            : new BuildContextMapResult(recorded.Value.Repo, recorded.Value.Draft, recorded.Value.Proposed, [.. report.Degraded.Take(MaxDegraded)]);
    }
}
