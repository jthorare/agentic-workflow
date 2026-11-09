using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Host.Models;

/// <summary>Posts an 'ask_owner' node's question as a 'workflow.question' event, which the needs-you view shows until it is answered or its run ends. The key is the run and node, so asking again adds nothing.</summary>
/// <param name="ledger">Appends the event.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="callers">The caller, when there is one.</param>
/// <param name="scrubber">Scrubs the question, choices and default before the ledger stores them.</param>
internal sealed class LedgerQuestionPort(ILedgerStore ledger, HeldLease held, EventFactory events, ICallerContextAccessor callers, ITextScrubber scrubber) : IQuestionPort
{
    public async ValueTask<Result<Unit>> Post(QuestionAsk ask, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ask);
        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var by = callers.Current?.Actor ?? "service:workflow";
        var entry = events.Entry(EventTypes.WorkflowQuestion, new WorkflowQuestionPayload(ask.Run, ask.Node, scrubber.Scrub(ask.Question), [.. ask.Choices.Select(scrubber.Scrub)], ask.Default is null ? null : scrubber.Scrub(ask.Default), ask.TimeoutAt, by), $"{EventTypes.WorkflowQuestion}:{ask.Run.Value}:{ask.Node.Value}");
        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : Unit.Value;
    }
}
