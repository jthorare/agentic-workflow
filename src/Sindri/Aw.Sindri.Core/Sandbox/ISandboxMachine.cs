namespace Aw.Sindri.Core.Sandbox;

/// <summary>What this machine has, for the sandbox's limits (doctor's limits row): memory and cores as the process sees them.</summary>
public interface ISandboxMachine
{
    /// <summary>Gets the memory the process may use, in bytes (the cgroup or container limit when there is one).</summary>
    public long RamBytes { get; }

    /// <summary>Gets the logical cores.</summary>
    public int Cores { get; }
}
