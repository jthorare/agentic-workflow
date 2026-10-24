using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.SetPromptSort;

internal sealed class SetPromptSortService : IService<SetPromptSortRequest, SetPromptSortResponse>
{
    public ValueTask<Result<SetPromptSortResponse>> Handle(SetPromptSortRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("SetPromptSort: make SetPromptSortTests pass (cutover unit 6.1)");
}
