namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>The memory the system charges a set of processes (macOS 'phys_footprint', SB-9c): what the Activity Monitor calls "Memory", which counts a shared page once.</summary>
internal interface IPhysFootprint
{
    /// <summary>Adds up the footprint of the given processes.</summary>
    /// <param name="pids">The process ids.</param>
    /// <returns>The bytes; null when it can't be read (not macOS, or no process could be asked), so the caller falls back to resident sets.</returns>
    public long? Sum(IReadOnlyList<int> pids);
}
