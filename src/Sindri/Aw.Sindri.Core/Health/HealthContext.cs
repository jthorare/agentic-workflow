namespace Aw.Sindri.Core.Health;

/// <summary>What <see cref="HealthSummary.For"/> reads besides the views and the services.</summary>
/// <param name="CodeIndex">The code index's problems, one per repo at most, then the context sources' (one per source that needs a look, #4010), then the session collector's (a failed source or a collector that can't run, #4012); they follow the services' and come before the profile's.</param>
/// <param name="Now">The clock's reading, for how long ago a service failed; without it the problem names no age.</param>
public sealed record HealthContext(IReadOnlyList<HealthProblem>? CodeIndex, DateTimeOffset? Now = null);
