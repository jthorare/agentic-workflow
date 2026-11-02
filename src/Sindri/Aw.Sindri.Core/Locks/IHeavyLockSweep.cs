namespace Aw.Sindri.Core.Locks;

/// <summary>Clears the litter a killed sandbox scope leaves in the heavy wrapper's lock folder (sandbox spec §3.3 rule 6, §15 item 8 "Stale slots").</summary>
public interface IHeavyLockSweep
{
    /// <summary>Removes every 'heavy-session/*.lock' slot whose owner and key process are both gone; live slots, slots too new to have an owner record, and 'heavy-job.lock*' are left alone.</summary>
    /// <param name="locksDir">The lock folder (&lt;state root&gt;/locks).</param>
    /// <returns>How many slots were removed.</returns>
    public int Sweep(string locksDir);
}
