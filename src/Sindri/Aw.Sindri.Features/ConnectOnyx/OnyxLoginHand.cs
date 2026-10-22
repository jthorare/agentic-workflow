using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.ConnectOnyx;

/// <summary>The per-request hand-over (see <see cref="IOnyxLoginHand"/>): scoped to the request, so it dies with it.</summary>
internal sealed class OnyxLoginHand : IOnyxLoginHand
{
    private OnyxLogin? _login;

    public void Give(OnyxLogin login)
    {
        ArgumentNullException.ThrowIfNull(login);
        _login = login;
    }

    public OnyxLogin? Take()
    {
        var login = _login;
        _login = null;
        return login;
    }
}
