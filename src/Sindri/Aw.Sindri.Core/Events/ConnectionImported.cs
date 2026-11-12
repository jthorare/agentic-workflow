namespace Aw.Sindri.Core.Events;

/// <summary>The 'connection.imported' payload (#3364): the Linear key kept at the profile's old pointer became a connection. The key is not in it.</summary>
/// <param name="Connection">The connection name ('linear:default').</param>
/// <param name="Source">The pointer it was read from ('keychain:sindri-index/linear').</param>
/// <param name="At">When it was imported.</param>
public sealed record ConnectionImported(string Connection, string Source, DateTimeOffset At);
