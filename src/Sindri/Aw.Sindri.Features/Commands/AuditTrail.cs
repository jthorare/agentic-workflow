using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Features.Commands;

/// <summary>
/// Writes a command attempt's 'command.handled' audit row (API spec §2.1, §3.4) under the process's lease grant, keyed
/// 'command.handled:&lt;commandId&gt;' so a replay is a no-op. Without a grant the row can't be written (SND-LEDGER-002).
/// </summary>
/// <param name="held">The grant this process holds.</param>
/// <param name="ledger">The ledger.</param>
/// <param name="events">The event factory (the caller's actor and correlation).</param>
/// <param name="ids">Ids for an attempt with no caller context (it has no command id of its own).</param>
/// <param name="logger">Where a lost audit row is reported.</param>
internal sealed partial class AuditTrail(HeldLease held, ILedgerStore ledger, EventFactory events, IIdGenerator ids, ILogger<AuditTrail> logger)
{
    /// <summary>Appends the audit row of one attempt.</summary>
    /// <param name="caller">The caller, or null ('none').</param>
    /// <param name="row">The row the attempt ran as.</param>
    /// <param name="outcome">How it ended.</param>
    /// <param name="code">The error code when it didn't succeed.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or the ledger's error (SND-LEDGER-002 without a grant).</returns>
    internal async ValueTask<Result<Unit>> Record(CallerContext? caller, CommandRow row, CommandOutcome outcome, string? code, CancellationToken cancellationToken)
    {
        if (held.Current is not { } grant)
        {
            // why: only an accepted command needed the lease; a refused, invalid or failed attempt without one isn't the process's fault
            var fix = outcome == CommandOutcome.Accepted ? " (fix: restart sindri)" : "";
            return SindriErrors.LedgerUnavailable.With($"no lease is held, so the audit row for {row.Action.Value} can't be written{fix}");
        }

        var commandId = caller?.CommandId ?? ids.New<CommandId>();
        var payload = new CommandHandled(commandId, row.Action, row.Class, caller?.Actor ?? "none", outcome, code);
        var key = $"{EventTypes.CommandHandled}:{commandId.Value}";
        var appended = await ledger.Append(events.Entry(EventTypes.CommandHandled, payload, key), grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : Unit.Value;
    }

    /// <summary>Reports that an attempt's audit row wasn't written.</summary>
    /// <param name="row">The command's row.</param>
    /// <param name="outcome">How the attempt ended: an accepted command's result stands, any other attempt answers SND-LEDGER-002.</param>
    /// <param name="error">Why the row wasn't written.</param>
    internal void Lost(CommandRow row, CommandOutcome outcome, Error error)
    {
        if (outcome == CommandOutcome.Accepted)
        {
            LogLost(logger, row.Action.Value, error.Code, error.Message);
            return;
        }

        LogLostAttempt(logger, row.Action.Value, Name(outcome), error.Code, error.Message);
    }

    private static string Name(CommandOutcome outcome) => outcome switch
    {
        CommandOutcome.Refused => "refused",
        CommandOutcome.Invalid => "invalid",
        CommandOutcome.Failed => "failed",
        CommandOutcome.Accepted => "accepted",
        _ => outcome.ToString(),
    };

    [LoggerMessage(Level = LogLevel.Error, Message = "SND-LEDGER-002 the audit row for {Action} wasn't written ({Code} {Detail}); the command's own result stands")]
    private static partial void LogLost(ILogger logger, string action, string code, string detail);

    [LoggerMessage(Level = LogLevel.Error, Message = "SND-LEDGER-002 the audit row for {Action} ({Outcome}) wasn't written ({Code} {Detail}); the caller gets SND-LEDGER-002")]
    private static partial void LogLostAttempt(ILogger logger, string action, string outcome, string code, string detail);
}
