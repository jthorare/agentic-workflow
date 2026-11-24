using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.Commands;

/// <summary>
/// The chain's audit (API spec §2.1): every attempt that passes the gate gets one 'command.handled' row (accepted,
/// refused, invalid or failed) after the rest of the chain ran, plus a 'sindri.command &lt;action&gt;' span and a count.
/// An accepted self-audited row (StopProcess: its 'process.stopped' is the audit) writes none. A lost row after an
/// accepted command is logged and the result stands; on any other outcome it is logged too and the caller gets one
/// SND-LEDGER-002 (the attempt's reason first, neither part repeating that code). A service
/// caller holds one context for a whole run, so each command attempt under any service-class caller gets its own
/// command id here, replacing the run's (the correlation is kept): a second command in the same run is a new row, not
/// a replay of the first.
/// </summary>
/// <param name="inner">The rest of the chain.</param>
/// <param name="audit">Writes the row.</param>
/// <param name="callers">The caller of this attempt.</param>
/// <param name="catalog">The command catalog the attempt is classified by.</param>
/// <param name="ids">Mints a service attempt's command id.</param>
/// <typeparam name="TRequest">The command record.</typeparam>
/// <typeparam name="TResponse">What it answers.</typeparam>
internal sealed class AuditingService<TRequest, TResponse>(IService<TRequest, TResponse> inner, AuditTrail audit, ICallerContextAccessor callers,
    ICommandCatalog catalog, IIdGenerator ids) : Decorated<TRequest, TResponse>(inner)
    where TRequest : notnull
{
    /// <summary>The service it wraps (for the chain-order test).</summary>
    internal IService<TRequest, TResponse> Next => Inner;

    public override async ValueTask<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken)
    {
        var row = ActionClassifier.Classify(catalog, request);
        using var attempt = EnterAttempt();
        var caller = callers.Current;
        using var span = CommandTelemetry.Source.StartActivity("sindri.command " + row.Action.Value);
        var result = await Inner.Handle(request, cancellationToken).ConfigureAwait(false);
        var outcome = CommandOutcomes.Of(result.IsError ? result.Error : null);
        CommandTelemetry.Attempt(span, row.Class, caller?.Actor ?? "none", outcome);
        if (outcome == CommandOutcome.Accepted && row.SelfAudited)
        {
            return result;
        }

        // The command has run: its row is written even when a stop cancelled the request meanwhile, so no effect goes unaudited.
        // A self-audited command may have written its accepted row already (RestartProcess, #4024): any other outcome goes under the ':final' key, so it isn't a replay of that row.
        var code = result.IsError ? result.Error.Code : null;
        var recorded = row.SelfAudited
            ? await audit.RecordFinal(caller, row, outcome, code, CancellationToken.None).ConfigureAwait(false)
            : await audit.Record(caller, row, outcome, code, CancellationToken.None).ConfigureAwait(false);
        if (recorded.IsSuccess)
        {
            return result;
        }

        audit.Lost(row, outcome, recorded.Error);
        if (outcome == CommandOutcome.Accepted)
        {
            return result;
        }

        // The attempt's own reason comes first: a stop that failed has already cleared the grant the row needs.
        return SindriErrors.LedgerUnavailable.With($"{Part(result.Error)}; and its audit row couldn't be written: {Part(recorded.Error)}");
    }

    /// <summary>An error as part of an SND-LEDGER-002 message: its code, unless it is SND-LEDGER-002 itself, then its message.</summary>
    private static string Part(Error error) =>
        string.Equals(error.Code, SindriErrors.LedgerUnavailable.Code, StringComparison.Ordinal) ? error.Message : $"{error.Code} {error.Message}";

    /// <summary>
    /// A service caller's attempt under its own command id, which replaces the run's for the attempt (the correlation
    /// is kept, and disposing restores the run's context); null, nothing entered, for any other caller.
    /// </summary>
    private IDisposable? EnterAttempt() => callers.Current is { Class: CallerClass.Service } service
        ? callers.Enter(service with { CommandId = ids.New<CommandId>() })
        : null;
}
