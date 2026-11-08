using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Features.TestConnection;

/// <summary>
/// Tests a connection: reads its secret and calls the probe URL (the connection's Config 'probeUrl', else its preset's). An unknown name is SND-CONN-001.
/// A rejected credential sets status 'error' (or 'needs-sign-in' for an oauth connection) and a reason; the answer is Ok false, not a failed call.
/// </summary>
/// <param name="store">The connections.</param>
/// <param name="secrets">Reads the tokens.</param>
/// <param name="presets">The shipped presets.</param>
/// <param name="probe">Calls the probe URL.</param>
/// <param name="time">The clock.</param>
internal sealed class TestConnectionService(IConnectionStore store, IConnectionSecrets secrets, IConnectionPresetCatalog presets, IConnectionProbe probe, TimeProvider time)
    : IService<TestConnectionRequest, TestConnectionResponse>
{
    private readonly IConnectionStore _store = store;
    private readonly IConnectionSecrets _secrets = secrets;
    private readonly IConnectionPresetCatalog _presets = presets;
    private readonly IConnectionProbe _probe = probe;
    private readonly TimeProvider _time = time;

    public ValueTask<Result<TestConnectionResponse>> Handle(TestConnectionRequest request, CancellationToken cancellationToken) => throw Pending();

    // A red stub: the ports are held so the tests build the class the way the green one is built.
    private NotSupportedException Pending()
    {
        _ = (_store, _secrets, _presets, _probe, _time);
        return new NotSupportedException();
    }
}
