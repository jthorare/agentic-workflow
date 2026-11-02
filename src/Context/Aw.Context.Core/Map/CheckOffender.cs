namespace Aw.Context.Core.Map;

/// <summary>A feature that trips a structure measure, for the human-only 'context map checks' output (never in a stored reason or a job result).</summary>
/// <param name="Measure">The measure's name.</param>
/// <param name="Feature">The feature's key.</param>
/// <param name="Files">How many files it holds.</param>
public sealed record CheckOffender(string Measure, string Feature, int Files);
