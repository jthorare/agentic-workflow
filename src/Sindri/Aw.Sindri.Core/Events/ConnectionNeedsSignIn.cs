namespace Aw.Sindri.Core.Events;

/// <summary>
/// The 'connection.needs-sign-in' payload (connections PR 10b): a refresh failed and the owner has to sign in again. It holds the connection's name, the
/// reason as a short code (the provider's error code, for example 'invalid_grant', else the SND code) and when it happened. Never a token or a token prefix.
/// </summary>
/// <param name="Connection">The connection name ('linear:work').</param>
/// <param name="Reason">The reason code.</param>
/// <param name="At">When the refresh failed.</param>
public sealed record ConnectionNeedsSignIn(string Connection, string Reason, DateTimeOffset At);
