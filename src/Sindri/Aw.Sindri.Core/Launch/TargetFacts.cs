
namespace Aw.Sindri.Core.Launch;

/// <summary>One catalog target in 'launch.probed' (UI spec E-3, launcher L-4).</summary>
/// <param name="Id">The target.</param>
/// <param name="Present">True when its CLI is on the login PATH.</param>
/// <param name="Version">Its version, or null.</param>
/// <param name="Limited">Why it works with limits ('no-usage-reader', no Sindri tools), or null.</param>
/// <param name="Problem">Why it can't launch, or null.</param>
/// <param name="Source">Where the entry came from.</param>
/// <param name="Overridden">True when launcher.json replaced a built-in.</param>
public sealed record TargetFacts(AgentId Id, bool Present, string? Version, string? Limited, string? Problem, CatalogSource Source, bool Overridden);
