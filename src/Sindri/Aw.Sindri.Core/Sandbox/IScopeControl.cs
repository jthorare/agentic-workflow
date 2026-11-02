namespace Aw.Sindri.Core.Sandbox;

/// <summary>What the watchdog asks systemd about a session's scope (sandbox spec §3.3, §15 item 4): 'systemctl --user' over one unit.</summary>
public interface IScopeControl
{
    /// <summary>Reads the scope's cgroup path.</summary>
    /// <param name="unit">The scope unit.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The path under the cgroup root (for example '/user.slice/…/sindri-LCH-….scope'), or null when systemd does not know the unit.</returns>
    public Task<string?> ControlGroup(string unit, CancellationToken cancellationToken);

    /// <summary>Reads the unit's 'Result' property: 'oom-kill' after a memory kill, until the unit is reset.</summary>
    /// <param name="unit">The scope unit.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The result, or null when it could not be read.</returns>
    public Task<string?> Result(string unit, CancellationToken cancellationToken);

    /// <summary>Signals every process of the scope.</summary>
    /// <param name="unit">The scope unit.</param>
    /// <param name="signal">The signal name, for example 'SIGTERM'.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when systemctl accepted it.</returns>
    public Task<bool> Kill(string unit, string signal, CancellationToken cancellationToken);

    /// <summary>Moves the scope's loose RuntimeMaxSec (a runtime property only).</summary>
    /// <param name="unit">The scope unit.</param>
    /// <param name="ceiling">The new ceiling, counted from the scope's start.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when systemctl accepted it.</returns>
    public Task<bool> SetRuntimeMax(string unit, TimeSpan ceiling, CancellationToken cancellationToken);
}
