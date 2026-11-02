using Aw.Judge.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.GetLabelStatus;

internal sealed class GetLabelStatusService(IEvalStore items) : IService<GetLabelStatusRequest, GetLabelStatusResponse>
{
    public async ValueTask<Result<GetLabelStatusResponse>> Handle(GetLabelStatusRequest request, CancellationToken cancellationToken)
    {
        var counts = await items.Counts(cancellationToken).ConfigureAwait(false);
        if (counts.IsError)
        {
            return counts.Error;
        }

        var questions = new List<QuestionLabelStatus>();
        foreach (var count in counts.Value)
        {
            var agreement = await items.Agreement(count.Question, cancellationToken).ConfigureAwait(false);
            if (agreement.IsError)
            {
                return agreement.Error;
            }

            questions.Add(new QuestionLabelStatus(count.Question, count.Items, count.Labeled, count.Skipped, agreement.Value));
        }

        return new GetLabelStatusResponse(questions);
    }
}
