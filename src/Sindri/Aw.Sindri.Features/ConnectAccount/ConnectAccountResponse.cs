namespace Aw.Sindri.Features.ConnectAccount;

/// <summary>Where the account stands after the call (first-run spec §4.2's row states).</summary>
/// <param name="Provider">github, linear or slack.</param>
/// <param name="State">`connected` (a gh login), `code` (the device code is shown, Sindri polls GitHub), `token-needed` (no sign-in is possible: <paramref name="Reason"/> says why), or `disconnected`.</param>
/// <param name="UserCode">For `code`: the code the owner types at GitHub, such as ABCD-1234. Not a secret: the device code behind it never leaves Sindri.</param>
/// <param name="VerificationUri">For `code`: the page the owner types it on.</param>
/// <param name="ExpiresAt">For `code`: when GitHub stops taking the code.</param>
/// <param name="Reason">For `token-needed`: the error code that sends the owner to the token paste, SND-SETUP-013 or SND-SETUP-014.</param>
internal sealed record ConnectAccountResponse(string Provider, string State, string? UserCode = null, string? VerificationUri = null, DateTimeOffset? ExpiresAt = null, string? Reason = null);
