using Aw.Kernel;
using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Host.Commands;

internal sealed partial class ControlPlaneClient
{
    private Result<(ControlEndpoint Endpoint, Ping Ping)>? _found;

    /// <summary>Records a control plane the caller just proved is up, so this client's next call doesn't ping it again (#3727).</summary>
    /// <param name="found">The probe's successful answer.</param>
    internal void Remember((ControlEndpoint Endpoint, Ping Ping) found) => _found = found;

    /// <summary>The control plane: the answer of an earlier successful ping of this process, else a ping now. A failed call forgets it.</summary>
    private async ValueTask<Result<(ControlEndpoint Endpoint, Ping Ping)>> Find(CancellationToken ct)
    {
        if (_found is { } known)
        {
            return known;
        }

        var found = await probe.Check(ct).ConfigureAwait(false);
        if (found.IsSuccess)
        {
            _found = found;
        }

        return found;
    }
}
