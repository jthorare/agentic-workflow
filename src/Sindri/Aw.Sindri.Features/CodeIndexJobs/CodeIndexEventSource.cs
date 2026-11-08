using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.CodeIndexJobs;

/// <summary>Opens the code index's event recorder under the lease the control plane holds.</summary>
/// <param name="held">The lease the events are fenced by.</param>
/// <param name="ledger">Appends the events.</param>
/// <param name="events">Builds the entries.</param>
internal sealed class CodeIndexEventSource(HeldLease held, ILedgerStore ledger, EventFactory events)
{
    /// <summary>Appends 'codeindex.built'; <paramref name="attempt"/> (the job's id) keeps one build of a commit apart from another's.</summary>
    internal async ValueTask<Result<LedgerAppend>> Built(CodeIndexBuilt payload, string attempt, CancellationToken cancellationToken)
    {
        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var key = $"{EventTypes.CodeIndexBuilt}:{payload.Repo}:{payload.Commit}:{attempt}";
        return await ledger.Append(events.Entry(EventTypes.CodeIndexBuilt, payload, key), grant, cancellationToken).ConfigureAwait(false);
    }
}
