namespace Aw.Sindri.Core.Sandbox;

/// <summary>Where the watchdog writes down a memory kill it made itself, for a platform whose kernel has no memory limit (macOS): the one fact the scope's accounting would otherwise hold ('oom_kill').</summary>
public interface IMemoryKills
{
    /// <summary>Records that the watchdog killed the scope for reaching its memory ceiling.</summary>
    /// <param name="unit">The scope unit.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when the record was written.</returns>
    public Task<bool> Record(string unit, CancellationToken cancellationToken);
}
