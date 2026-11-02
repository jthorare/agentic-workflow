using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.EvaluateSort;

internal sealed class EvaluateSortService : IService<EvaluateSortRequest, EvaluateSortResponse>
{
    public ValueTask<Result<EvaluateSortResponse>> Handle(EvaluateSortRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("EvaluateSort: make EvaluateSortTests pass (cutover unit 6.4)");
}
