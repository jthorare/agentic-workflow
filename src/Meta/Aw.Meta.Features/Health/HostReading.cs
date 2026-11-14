using Aw.Meta.Core.Health;

namespace Aw.Meta.Features.Health;

/// <summary>Everything read from the host in one go.</summary>
/// <param name="Snapshot">The processes.</param>
/// <param name="Resources">Memory, swap and disk.</param>
/// <param name="Locks">The taken heavy-job lock slots.</param>
/// <param name="OomPolicy">The warning when systemd's DefaultOOMPolicy is known and is not continue; null otherwise.</param>
/// <param name="StaleScripts">The warning when an installed heavy or meta-gate-reserve differs from the checkout's; null otherwise.</param>
internal sealed record HostReading(HostSnapshot Snapshot, HostResources Resources, IReadOnlyList<HeavyLock> Locks, string? OomPolicy = null, string? StaleScripts = null);
