using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.AnswerQuestion;

/// <summary>Answers a question: checks the run is still going, hands the answer to its workflow, and records who answered.</summary>
/// <param name="runs">The runs.</param>
/// <param name="answerer">Delivers the answer to the run's workflow.</param>
/// <param name="ledger">Appends 'workflow.answered'.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entry.</param>
internal sealed class AnswerQuestionService(IWorkflowRunStore runs, IGraphRunAnswerer answerer, ILedgerStore ledger, HeldLease held, EventFactory events) : IService<AnswerQuestionRequest, AnswerQuestionResponse>
{
    public ValueTask<Result<AnswerQuestionResponse>> Handle(AnswerQuestionRequest request, CancellationToken cancellationToken)
    {
        _ = (runs, answerer, ledger, held, events);
        throw new NotSupportedException();
    }
}
