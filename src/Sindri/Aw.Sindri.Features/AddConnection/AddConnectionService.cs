using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;

namespace Aw.Sindri.Features.AddConnection;

/// <summary>
/// Adds a connection. A bad name is SND-CONN-008, an existing one SND-CONN-010, a preset without a client id SND-CONN-007. An oauth connection starts the
/// sign-in; its row is written only after the tokens are stored, so a denied consent (SND-CONN-009), a timeout (SND-CONN-003) or a state mismatch
/// (SND-CONN-004) leaves no row and no secret. A rest or cli connection reads --key-env once and stores it as the secret.
/// </summary>
/// <param name="store">The connections.</param>
/// <param name="secrets">Stores the tokens.</param>
/// <param name="presets">The shipped presets.</param>
/// <param name="signIn">Starts the browser sign-in.</param>
/// <param name="environment">Reads --key-env.</param>
/// <param name="time">The clock.</param>
/// <param name="callers">Who is adding.</param>
internal sealed class AddConnectionService(IConnectionStore store, IConnectionSecrets secrets, IConnectionPresetCatalog presets, ISignInSession signIn, IEnvironmentReader environment, TimeProvider time, ICallerContextAccessor callers)
    : IService<AddConnectionRequest, AddConnectionResponse>
{
    private readonly IConnectionStore _store = store;
    private readonly IConnectionSecrets _secrets = secrets;
    private readonly IConnectionPresetCatalog _presets = presets;
    private readonly ISignInSession _signIn = signIn;
    private readonly IEnvironmentReader _environment = environment;
    private readonly TimeProvider _time = time;
    private readonly ICallerContextAccessor _callers = callers;

    public ValueTask<Result<AddConnectionResponse>> Handle(AddConnectionRequest request, CancellationToken cancellationToken) => throw Pending();

    // A red stub: the ports are held so the tests build the class the way the green one is built.
    private NotSupportedException Pending()
    {
        _ = (_store, _secrets, _presets, _signIn, _environment, _time, _callers);
        return new NotSupportedException();
    }
}
