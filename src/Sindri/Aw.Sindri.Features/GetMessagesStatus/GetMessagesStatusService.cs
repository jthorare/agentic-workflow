using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.GetMessagesStatus;

/// <summary>A stub until M1f (messages spec §14): the status route.</summary>
internal sealed class GetMessagesStatusService : IService<GetMessagesStatusRequest, GetMessagesStatusResponse>
{
    public ValueTask<Result<GetMessagesStatusResponse>> Handle(GetMessagesStatusRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("GetMessagesStatus is not built yet: messages spec §14 phase M1f");
}
