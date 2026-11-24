using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ConnectAccount;

/// <summary>Disconnect one account of the accounts question (`DELETE /sindri/setup/accounts/{provider}`, first-run spec §4.2). The owner's, checked by the per-start human token.</summary>
/// <param name="Provider">github (linear and slack come with their sign-ins).</param>
internal sealed record DisconnectAccountRequest(string Provider) : ICommand<ConnectAccountResponse>;
