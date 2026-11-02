namespace Aw.Sindri.Core.Sandbox;

/// <summary>Reads a scope's accounting files while the scope is alive; they go with it (sandbox spec §15 item 4), so the caller keeps the last sample.</summary>
public interface ICgroupStats
{
    /// <summary>Reads memory.events, memory.current, memory.peak, pids.current, pids.events, pids.peak and cpu.stat.</summary>
    /// <param name="cgroupPath">The scope's cgroup path under the cgroup root.</param>
    /// <returns>The sample, or null when the scope is gone (its files are not there). Never throws for that.</returns>
    public CgroupSample? Read(string cgroupPath);
}
