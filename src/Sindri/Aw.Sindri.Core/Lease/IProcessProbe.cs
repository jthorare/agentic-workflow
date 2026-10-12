namespace Aw.Sindri.Core.Lease;

/// <summary>The operating system's view of processes on this machine (port; the adapter reads pids and the boot id).</summary>
public interface IProcessProbe
{
    /// <summary>This process, as a lease holder.</summary>
    /// <returns>The current process's holder record.</returns>
    public LeaseHolder Self();

    /// <summary>What the system says about <paramref name="pid"/> on this machine now.</summary>
    /// <param name="pid">The process id.</param>
    /// <returns>Whether it runs and when it started.</returns>
    public ProcessFacts Facts(int pid);
}
