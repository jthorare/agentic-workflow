namespace Aw.Meta.Core.Ports;

/// <summary>A claim that a pid still belongs to the process a session or lock file was written for.</summary>
/// <param name="Pid">The process id the file names.</param>
/// <param name="RecordedAt">When the file was last written (see <see cref="Health.ProcessStart"/>).</param>
public sealed record ProcessClaim(int Pid, DateTimeOffset RecordedAt);
