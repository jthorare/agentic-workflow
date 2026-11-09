using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Features.AnswerQuestion;

/// <summary>
/// Answers a question: an answer is a text or a choice (not both), the run must still be going, and the workflow decides whether the node is waiting and takes it as one of its choices.
/// Once the workflow took the answer, 'workflow.answered' records who gave it (the caller's actor: 'human:&lt;client&gt;' for a person, 'session:launch:&lt;launch id&gt;' for a session Sindri launched, 'session:&lt;client&gt;' for any other session).
/// </summary>
/// <param name="runs">The runs.</param>
/// <param name="answerer">Delivers the answer to the run's workflow.</param>
/// <param name="ledger">Appends 'workflow.answered'.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="scrubber">Scrubs the answer before the ledger stores it; the workflow gets the answer as given.</param>
internal sealed class AnswerQuestionService(IWorkflowRunStore runs, IGraphRunAnswerer answerer, ILedgerStore ledger, HeldLease held, EventFactory events, ITextScrubber scrubber) : IService<AnswerQuestionRequest, AnswerQuestionResponse>
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

        var delivered = await answerer.Answer(new GraphAnswer(request.Run, request.Node, request.Text, request.Choice, request.By), cancellationToken).ConfigureAwait(false);
        if (delivered.IsError)
        {
            return delivered.Error;
        }

        var entry = events.Entry(EventTypes.WorkflowAnswered, new WorkflowAnsweredPayload(request.Run, request.Node, scrubber.Scrub(answer.Value), request.By), $"{EventTypes.WorkflowAnswered}:{request.Run.Value}:{request.Node.Value}");
        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError
            ? SindriErrors.ServiceFailed.With($"the workflow took the answer but the ledger could not record it: {appended.Error.Message}")
            : new AnswerQuestionResponse(request.Run, request.Node, answer.Value, request.By);
    }

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
