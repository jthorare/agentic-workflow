using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ConnectAccount;

/// <summary>
/// Connect one account of the accounts question (first-run spec decision 13 to 17, `POST /sindri/setup/accounts/{provider}`): github, linear or slack. The owner's, checked by the per-start human token.
/// The body of a pasted token is a secret type that goes straight to the secret store; this stub carries only the provider.
/// </summary>
/// <param name="Provider">github, linear or slack.</param>
internal sealed record ConnectAccountRequest(string Provider) : ICommand<ConnectAccountResponse>;
