namespace Aw.Sindri.Core.Launch;

/// <summary>The size a host gives a new session's window (agents spec §15.2: 'new-session -x W -y H').</summary>
/// <param name="Cols">Columns.</param>
/// <param name="Rows">Rows.</param>
public sealed record HostSize(int Cols, int Rows);
