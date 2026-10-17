namespace Aw.Sindri.Core.Onyx;

/// <summary>What one readiness probe of the running Onyx found.</summary>
/// <param name="Healthy">The health route answered 2xx.</param>
/// <param name="BackendVersion">The release the backend reports, or null when it isn't healthy yet or the version answer couldn't be read.</param>
public sealed record OnyxProbe(bool Healthy, string? BackendVersion);
