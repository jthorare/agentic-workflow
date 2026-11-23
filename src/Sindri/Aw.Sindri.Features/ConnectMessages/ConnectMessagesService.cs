using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ConnectMessages;

/// <summary>A stub until M1d (messages spec §14): the prefix check (SND-MSG-030) before any call, auth.test, the workspace check (SND-MSG-024) and the store.</summary>
internal sealed class ConnectMessagesService : IService<ConnectMessagesRequest, ConnectMessagesResponse>
{
    public ValueTask<Result<ConnectMessagesResponse>> Handle(ConnectMessagesRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("ConnectMessages is not built yet: messages spec §14 phase M1d");
}
