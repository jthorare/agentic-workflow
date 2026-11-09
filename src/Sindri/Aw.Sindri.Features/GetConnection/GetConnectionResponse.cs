using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Features.GetConnection;

/// <summary>One connection.</summary>
/// <param name="Connection">The view (never a token or a token prefix).</param>
/// <param name="LastSignInFailure">How the last sign-in of a connected row failed, or null: the row still works on its old tokens, so the failure is shown beside it rather than instead of it.</param>
public sealed record GetConnectionResponse(ConnectionView Connection, SignInFailureView? LastSignInFailure = null);
