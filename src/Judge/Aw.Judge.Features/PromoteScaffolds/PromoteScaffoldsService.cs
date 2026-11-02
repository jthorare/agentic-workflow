using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.PromoteScaffolds;

internal sealed class PromoteScaffoldsService : IService<PromoteScaffoldsRequest, PromoteScaffoldsResponse>
{
    public ValueTask<Result<PromoteScaffoldsResponse>> Handle(PromoteScaffoldsRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("PromoteScaffolds: make PromoteScaffoldsTests pass (cutover unit 6.4)");
}
