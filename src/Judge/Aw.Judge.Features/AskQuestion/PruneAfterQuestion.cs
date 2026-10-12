using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.AskQuestion;

// Retention, the one cross-cutting step after a question (the only command that writes decision details): after a
// successful question, drop the details older than DecisionDetails.Retention before the start of today (UTC). The
// cutoff moves once a day, so after the day's first prune there is nothing older and the log answers from a read,
// without a write lock. Best effort: the question's result never depends on it.
internal sealed class PruneAfterQuestion(IService<AskQuestionRequest, AskQuestionResponse> inner, IDecisionLog decisions, TimeProvider clock)
    : Decorated<AskQuestionRequest, AskQuestionResponse>(inner)
{
    public override async ValueTask<Result<AskQuestionResponse>> Handle(AskQuestionRequest request, CancellationToken cancellationToken)
    {
        var answer = await Inner.Handle(request, cancellationToken).ConfigureAwait(false);
        if (answer.IsSuccess)
        {
            _ = await decisions.PruneDetailsBefore(Cutoff(clock.GetUtcNow()), cancellationToken).ConfigureAwait(false);   // fails open
        }

        return answer;
    }

    private static DateTimeOffset Cutoff(DateTimeOffset now) => new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero) - DecisionDetails.Retention;
}
