using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Features.ConnectMessages;

/// <summary>
/// Connect Slack for Messages (messages spec §5.1). Built so far: the prefix check (SND-MSG-030) before any call. Still a stub until M1d's second PR:
/// the manifest answer, auth.test, the workspace check (SND-MSG-024) and the store.
/// </summary>
internal sealed class ConnectMessagesService : IService<ConnectMessagesRequest, ConnectMessagesResponse>
{
    public ValueTask<Result<ConnectMessagesResponse>> Handle(ConnectMessagesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.Manifest)
        {
            var token = SlackUserToken.Check(request.Token);
            if (token.IsError)
            {
                return ValueTask.FromResult<Result<ConnectMessagesResponse>>(token.Error);
            }
        }

        throw new NotSupportedException("ConnectMessages is not built yet: messages spec §14 phase M1d (auth.test and the store)");
    }
}
