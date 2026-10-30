using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Host.Commands.SessionWrap;

/// <summary>What 'session wrap --limits' watches the scope through.</summary>
/// <param name="Scope">systemd's view of the scope.</param>
/// <param name="Stats">The scope's accounting files.</param>
/// <param name="MemoryKills">Non-null on a platform without a kernel memory limit (macOS): the watchdog kills at the memory line itself and writes the kill down here. Null on Linux.</param>
internal sealed record WatchdogPorts(IScopeControl Scope, ICgroupStats Stats, IMemoryKills? MemoryKills = null);
