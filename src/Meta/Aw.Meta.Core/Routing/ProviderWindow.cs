namespace Aw.Meta.Core.Routing;

/// <summary>One usage window a provider reports.</summary>
/// <param name="Name">5h, weekly or the length in minutes.</param>
/// <param name="UsedPct">How much of it is used, 0 to 100; 0 once its reset time has passed.</param>
/// <param name="ResetsAt">When it resets; null when the provider does not say.</param>
public sealed record ProviderWindow(string Name, double UsedPct, DateTimeOffset? ResetsAt);
