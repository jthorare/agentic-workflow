using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.GetMessageThread;

/// <summary>A stub until M1f (messages spec §14): the thread route.</summary>
internal sealed class GetMessageThreadService : IService<GetMessageThreadRequest, GetMessageThreadResponse>
{
    public ValueTask<Result<GetMessageThreadResponse>> Handle(GetMessageThreadRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("GetMessageThread is not built yet: messages spec §14 phase M1f");
}
