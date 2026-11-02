namespace Aw.Sindri.Core.Sandbox;

/// <summary>What a platform without systemd scopes (macOS) drops once a session has ended and the watchdog has given its verdict: the unit's group id file, which would name an unrelated group once the system reuses the id.</summary>
public interface IScopeRelease
{
    /// <summary>Drops the unit's scope record. Best effort: a stale record is ignored once its group is gone.</summary>
    /// <param name="unit">The scope unit.</param>
    public void Release(string unit);
}
