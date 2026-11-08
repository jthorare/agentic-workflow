using Aw.Kernel;

namespace Aw.Sindri.Core.Connections.OAuth;

/// <summary>A sign-in under way.</summary>
/// <param name="ConsentUrl">The provider's consent page. An agent hands it to the owner when no browser can open.</param>
/// <param name="Completion">Ends with the tokens when the callback lands and the code is exchanged; with SND-CONN-003 after 5 minutes, SND-CONN-004 on a state mismatch, SND-CONN-009 when consent is denied.</param>
public sealed record SignInStarted(Uri ConsentUrl, Task<Result<TokenSet>> Completion);
