namespace Aw.Sindri.Core.Events;

/// <summary>The 'connection.removed' payload (#2460): a connection's secret and row were deleted. The envelope's actor is who removed it.</summary>
/// <param name="Connection">The connection name ('linear:work').</param>
/// <param name="At">When it was removed.</param>
public sealed record ConnectionRemoved(string Connection, DateTimeOffset At);
