namespace Aw.Sindri.Features.ConnectAccount;

/// <summary>Where the account stands after the call.</summary>
/// <param name="Provider">github, linear or slack.</param>
/// <param name="State">The account's state, such as `connected`.</param>
internal sealed record ConnectAccountResponse(string Provider, string State);
