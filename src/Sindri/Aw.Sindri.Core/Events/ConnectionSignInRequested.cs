namespace Aw.Sindri.Core.Events;

/// <summary>The 'connection.sign-in-requested' payload (#2460): someone started a sign-in for a stored connection. The envelope's actor is who; the row's creator stays the one who added it.</summary>
/// <param name="Connection">The connection name ('linear:work').</param>
/// <param name="At">When the sign-in started.</param>
public sealed record ConnectionSignInRequested(string Connection, DateTimeOffset At);
