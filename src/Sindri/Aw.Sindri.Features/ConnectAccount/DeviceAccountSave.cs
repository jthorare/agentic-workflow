using Aw.Kernel;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Features.ConnectAccount;

/// <summary>Stores a device-flow yes for <see cref="DeviceAccountSignIn"/>: the registration opens a scope per call and runs <see cref="DeviceAccountStore.Save"/> in it.</summary>
/// <param name="tokens">GitHub's tokens.</param>
/// <param name="client">The client id the sign-in used.</param>
/// <param name="actor">Who connected.</param>
/// <param name="cancellationToken">Cancels the write.</param>
/// <returns>Unit, or why nothing was stored.</returns>
internal delegate ValueTask<Result<Unit>> DeviceAccountSave(TokenSet tokens, string client, string actor, CancellationToken cancellationToken);
