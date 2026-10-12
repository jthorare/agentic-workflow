using Aw.Kernel;
using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Testing;

/// <summary>The endpoint record in memory; passes <see cref="EndpointFileContract"/>.</summary>
public sealed class InMemoryEndpointFile : IEndpointFile
{
    private ControlEndpoint? _endpoint;
    private bool _damaged;

    /// <summary>Damages the stored record, as a half-written or edited file would be.</summary>
    public void Damage() => _damaged = true;

    /// <inheritdoc/>
    public ValueTask<Result<ControlEndpoint?>> Read(CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<ControlEndpoint?>>(_damaged ? null : _endpoint);

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Write(ControlEndpoint endpoint, CancellationToken cancellationToken)
    {
        (_endpoint, _damaged) = (endpoint, false);
        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Remove(int pid, CancellationToken cancellationToken)
    {
        if (!_damaged && _endpoint?.Pid == pid)
        {
            _endpoint = null;
        }

        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }
}
