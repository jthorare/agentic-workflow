using System.Globalization;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The 'context-rebuild' background service (context spec §6.2, README X63): runs only when the profile names it, on its cadence.
/// It proposes the map from the repo into a draft (one build at a time, no heavy-job slot) and records 'context.map.built'. Nothing runs map build inside the command gate: until API P6 adds the build job,
/// this service is how a draft appears without the proposals route.
/// </summary>
internal sealed class ContextRebuildService(ContextScopes scopes, IContextEngine engine, ILedgerStore ledger, EventFactory events) : IScheduledService
{
    /// <summary>The service's name in the profile.</summary>
    internal static ServiceName ServiceId { get; } = ServiceName.From("context-rebuild");

    public ServiceName Identity() => ServiceId;

    public async ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        var built = scope.IsError ? scope.Error : await engine.BuildMap(progress: null, scope.Value, cancellationToken).ConfigureAwait(false);
        if (built.IsError)
        {
            return built.Error;
        }

        var report = built.Value;
        var payload = new ContextMapBuilt(report.Draft.Repo.Value, report.Draft.Number, report.Proposed.Count, report.Degraded);
        var key = string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ContextMapBuilt}:{run.Grant.Epoch}:{run.Slot:O}");
        var recorded = await ledger.Append(events.Entry(EventTypes.ContextMapBuilt, payload, key), run.Grant, cancellationToken).ConfigureAwait(false);
        return recorded.IsError ? recorded.Error : ScheduledResult.Worked;
    }
}
