namespace Aw.Sindri.Core.Judge;

/// <summary>One model a model-pick question may choose.</summary>
/// <param name="Key">'m1', 'm2', ... cheapest first: the question's answer.</param>
/// <param name="Model">The model id.</param>
/// <param name="Tier">The highest band the model sits in: 'cheap', 'standard' or 'strong'.</param>
public sealed record PickCandidate(string Key, string Model, string Tier);
