using Aw.Kernel;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Coordination;

/// <summary>
/// The one way to find the meta's current transcript (#2683). The meta's live session is the registered role's session when that
/// session is live, else the live session that carries the registered name (the meta after /clear, before any hook re-registered
/// it). Its transcript is found from the session id, so no stored offsets row or role row has to be up to date.
/// </summary>
/// <param name="messages">Holds the registered role.</param>
/// <param name="sessions">The live Claude sessions.</param>
/// <param name="locator">Finds a session's transcript file.</param>
public sealed class MetaTranscriptResolver(IMessageStore messages, ISessionDirectory sessions, ITranscriptLocator locator)
{
    /// <summary>Finds the meta's current transcript.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The transcript path, or null when no meta is registered or its transcript file doesn't exist.</returns>
    public async ValueTask<Result<string?>> Find(CancellationToken cancellationToken)
    {
        var role = await messages.Role(cancellationToken).ConfigureAwait(false);
        if (role.IsError)
        {
            return role.Error;
        }

        if (role.Value is not { } registered)
        {
            return Results.Success<string?>(value: null);
        }

        var live = await sessions.Live(cancellationToken).ConfigureAwait(false);
        var session = registered.Session;
        if (live.IsSuccess && live.Value.All(s => s.Session != registered.Session) && registered.Name.Length > 0)
        {
            var renamed = live.Value.FirstOrDefault(s => string.Equals(s.Name, registered.Name, StringComparison.Ordinal));
            if (renamed is not null)
            {
                session = renamed.Session;
            }
        }

        return await locator.Find(session, cancellationToken).ConfigureAwait(false);
    }
}
