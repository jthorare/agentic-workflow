using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.CheckTraits;

internal sealed class CheckTraitsService : IService<CheckTraitsRequest, CheckTraitsResponse>
{
    public ValueTask<Result<CheckTraitsResponse>> Handle(CheckTraitsRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("CheckTraits: make CheckTraitsTests pass (cutover unit 6.2)");
}
