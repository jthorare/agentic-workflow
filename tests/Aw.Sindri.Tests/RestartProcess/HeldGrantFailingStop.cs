using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.RestartProcess;

namespace Aw.Sindri.Tests.RestartProcess;

/// <summary>
/// A serve stop that writes the restart's accepted 'command.handled' row first, as ServeStopPort does, and then fails before it clears the lease grant (#4024):
/// the chain's failed row for the same command meets a grant that is still held.
/// </summary>
/// <param name="audit">Writes the restart's audit row.</param>
/// <param name="callers">The restart's caller.</param>
/// <param name="catalog">The command catalog the restart is classified by.</param>
internal sealed class HeldGrantFailingStop(AuditTrail audit, ICallerContextAccessor callers, ICommandCatalog catalog) : IServeStop
{
    internal static readonly Error Failure = SindriErrors.ApiInternal.With("the stop failed before it cleared the lease grant");

    public async ValueTask<Result<ServeStopped>> Stop(CancellationToken cancellationToken)
    {
        var row = ActionClassifier.Classify(catalog, new RestartProcessRequest());
        var recorded = await audit.Record(callers.Current, row, CommandOutcome.Accepted, code: null, CancellationToken.None);
        return recorded.IsError ? recorded.Error : Failure;
    }
}
