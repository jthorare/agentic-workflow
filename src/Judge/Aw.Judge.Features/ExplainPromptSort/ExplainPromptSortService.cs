using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.ExplainPromptSort;

internal sealed class ExplainPromptSortService : IService<ExplainPromptSortRequest, ExplainPromptSortResponse>
{
    public ValueTask<Result<ExplainPromptSortResponse>> Handle(ExplainPromptSortRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("ExplainPromptSort: make ExplainPromptSortTests pass (cutover unit 6.4)");
}
