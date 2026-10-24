using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.SortPrompt;

internal sealed class SortPromptService : IService<SortPromptRequest, SortPromptResponse>
{
    public ValueTask<Result<SortPromptResponse>> Handle(SortPromptRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("SortPrompt: make SortPromptTests pass (cutover unit 6.1)");
}
