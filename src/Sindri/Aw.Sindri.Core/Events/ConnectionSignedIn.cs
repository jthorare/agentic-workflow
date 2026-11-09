namespace Aw.Sindri.Core.Events;

/// <summary>The 'connection.signed-in' payload (connections PR 10b): a connection that needed a new sign-in is connected again.</summary>
/// <param name="Connection">The connection name ('linear:work').</param>
/// <param name="At">When it was connected again.</param>
public sealed record ConnectionSignedIn(string Connection, DateTimeOffset At);
