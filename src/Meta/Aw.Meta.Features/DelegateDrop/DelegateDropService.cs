using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.DelegateDrop;

/// <summary>Removes the delegation of one issue from the ledger and logs it.</summary>
internal sealed class DelegateDropService : IService<DelegateDropRequest, DelegateDropResponse>
{
    public ValueTask<Result<DelegateDropResponse>> Handle(DelegateDropRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
