using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.MoveReadPointer;

/// <summary>A stub until M1h (messages spec §14): the local read pointer.</summary>
internal sealed class MoveReadPointerService : IService<MoveReadPointerRequest, MoveReadPointerResponse>
{
    public ValueTask<Result<MoveReadPointerResponse>> Handle(MoveReadPointerRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("MoveReadPointer is not built yet: messages spec §14 phase M1h");
}
