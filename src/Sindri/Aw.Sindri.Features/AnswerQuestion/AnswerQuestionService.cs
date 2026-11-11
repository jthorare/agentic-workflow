using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.AnswerQuestion;

/// <summary>
/// Answers a question: an answer is a text or a choice (not both), the run must still be going, and the workflow decides whether the node is waiting and takes it as one of its choices.
/// Once the workflow took the answer, 'workflow.answered' records who gave it (the caller's actor: 'human:&lt;client&gt;' for a person, 'session:launch:&lt;launch id&gt;' for a session Sindri launched, 'session:&lt;client&gt;' for any other session).
/// </summary>
/// <param name="runs">The runs.</param>
/// <param name="views">Reads the needs-you row of a loop item's question, which records the workflow that waits for the answer.</param>
/// <param name="answerer">Delivers the answer to the run's workflow.</param>
/// <param name="ledger">Appends 'workflow.answered'; reads the item's 'workflow.question' when its needs-you row is not there yet.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="scrubber">Scrubs the answer before the ledger stores it; the workflow gets the answer as given.</param>
internal sealed class AnswerQuestionService(IWorkflowRunStore runs, IViewReader views, IGraphRunAnswerer answerer, ILedgerStore ledger, HeldLease held, EventFactory events, ITextScrubber scrubber) : IService<AnswerQuestionRequest, AnswerQuestionResponse>
{
    private const int AnswerMax = 4000;

    public async ValueTask<Result<AnswerQuestionResponse>> Handle(AnswerQuestionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var answer = AnswerOf(request);
        if (answer.IsError)
        {
            return answer.Error;
        }

        if (await RunRefusal(request, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        // why: the lease is checked before the answer is delivered, so a refusal leaves the question open and a retry works
        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        // why: a loop item's question waits in its batch's child workflow, whose id only the question knows (the batch size is known at run time), so it is read from the question's row
        var batch = await BatchOf(request, cancellationToken).ConfigureAwait(false);
        if (batch.IsError)
        {
            return batch.Error;
        }

        var delivered = await answerer.Answer(new GraphAnswer(request.Run, request.Node, request.Text, request.Choice, request.By) { Item = request.Item, BatchWorkflow = batch.Value }, cancellationToken).ConfigureAwait(false);
        if (delivered.IsError)
        {
            return delivered.Error;
        }

        var entry = events.Entry(EventTypes.WorkflowAnswered, new WorkflowAnsweredPayload(request.Run, request.Node, scrubber.Scrub(answer.Value), request.By) { Item = request.Item }, $"{EventTypes.WorkflowAnswered}:{request.Run.Value}:{request.Node.Value}{(request.Item is { } at ? ":" + at.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty)}");
        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError
            ? SindriErrors.ServiceFailed.With($"the workflow took the answer but the ledger could not record it: {appended.Error.Message}")
            : new AnswerQuestionResponse(request.Run, request.Node, answer.Value, request.By);
    }

    /// <summary>The workflow that waits for the answer: null for a node outside a loop (the run's own workflow), else the batch child its question recorded.</summary>
    private async ValueTask<Result<string?>> BatchOf(AnswerQuestionRequest request, CancellationToken cancellationToken)
    {
        if (request.Item is not { } item)
        {
            return (string?)null;
        }

        if (item < 1)
        {
            return SindriErrors.InvalidInput.With($"the item is {item.ToString(System.Globalization.CultureInfo.InvariantCulture)}; items of a loop count from 1");
        }

        var row = await views.Get<NeedsYouRowRecord>(ViewNames.NeedsYou, NeedsYouRowIds.WorkflowQuestion(request.Run, request.Node, item).Value, cancellationToken).ConfigureAwait(false);
        if (row.IsError)
        {
            return row.Error;
        }

        // why: the view is the cheap first read, but the projector may not have folded the question yet; the ledger is the truth
        var batch = row.Value?.BatchWorkflow;
        if (batch is null && row.Value is null)
        {
            var found = await BatchFromLedger(request, item, cancellationToken).ConfigureAwait(false);
            if (found.IsError)
            {
                return found.Error;
            }

            batch = found.Value;
        }

        return batch is not null
            ? batch
            : SindriErrors.GraphNotWaiting.With($"'{request.Node.Value}' asked nothing for item {item.ToString(System.Globalization.CultureInfo.InvariantCulture)} (or it was answered already)");
    }

    /// <summary>The batch workflow the item's latest 'workflow.question' names, or null when no question was asked or a later 'workflow.answered' closed it.</summary>
    private async ValueTask<Result<string?>> BatchFromLedger(AnswerQuestionRequest request, int item, CancellationToken cancellationToken)
    {
        var head = await ledger.Head(cancellationToken).ConfigureAwait(false);
        if (head.IsError)
        {
            return head.Error;
        }

        string? batch = null;
        var cursor = Math.Max(0, head.Value - TailWindow);
        while (true)
        {
            var page = await ledger.ReadAfter(cursor, PageSize, cancellationToken).ConfigureAwait(false);
            if (page.IsError)
            {
                return page.Error;
            }

            if (page.Value.Count == 0)
            {
                return batch;
            }

            foreach (var recorded in page.Value)
            {
                batch = Fold(recorded, request, item, batch);
            }

            cursor = page.Value[^1].Sequence;
        }
    }

    private string? Fold(LedgerEvent recorded, AnswerQuestionRequest request, int item, string? batch)
    {
        if (string.Equals(recorded.Envelope.Type, EventTypes.WorkflowQuestion, StringComparison.Ordinal))
        {
            var asked = events.Payload<WorkflowQuestionPayload>(recorded);
            return asked.IsSuccess && IsItem(asked.Value.Run, asked.Value.Node, asked.Value.Item, request, item) ? asked.Value.BatchWorkflow : batch;
        }

        if (string.Equals(recorded.Envelope.Type, EventTypes.WorkflowAnswered, StringComparison.Ordinal))
        {
            var answered = events.Payload<WorkflowAnsweredPayload>(recorded);
            return answered.IsSuccess && IsItem(answered.Value.Run, answered.Value.Node, answered.Value.Item, request, item) ? null : batch;
        }

        return batch;
    }

    private static bool IsItem(WorkflowRunId run, NodeId node, int? at, AnswerQuestionRequest request, int item) => run == request.Run && node == request.Node && at == item;

    private const int TailWindow = 100_000;
    private const int PageSize = 500;

    /// <summary>The answer: exactly one of text and choice, not blank and not over the cap.</summary>
    private static Result<string> AnswerOf(AnswerQuestionRequest request)
    {
        var text = string.IsNullOrWhiteSpace(request.Text) ? null : request.Text;
        var choice = string.IsNullOrWhiteSpace(request.Choice) ? null : request.Choice;
        if ((text ?? choice) is not { } answer || (text is not null && choice is not null))
        {
            return SindriErrors.InvalidInput.With("an answer is exactly one of --text and --choice");
        }

        return answer.Length > AnswerMax
            ? SindriErrors.InvalidInput.With($"the answer is {answer.Length} characters long; the most is {AnswerMax}")
            : answer;
    }

    /// <summary>Why the run takes no answer now (unknown, ended or not started), or null when it may.</summary>
    private async ValueTask<Error?> RunRefusal(AnswerQuestionRequest request, CancellationToken cancellationToken)
    {
        var run = await runs.Get(request.Run, cancellationToken).ConfigureAwait(false);
        if (run.IsError)
        {
            return run.Error;
        }

        if (run.Value.Status is WorkflowRunStatus.Succeeded or WorkflowRunStatus.Failed or WorkflowRunStatus.Cancelled)
        {
            return SindriErrors.GraphRunEnded.With($"the run '{request.Run.Value}' already ended; there is nothing to answer");
        }

        return run.Value.Status == WorkflowRunStatus.Queued
            ? SindriErrors.GraphNotWaiting.With($"the run '{request.Run.Value}' is queued; it has asked nothing yet")
            : null;
    }
}
