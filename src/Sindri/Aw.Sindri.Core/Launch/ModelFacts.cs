namespace Aw.Sindri.Core.Launch;

/// <summary>One model an agent offers (model-pick spec §3).</summary>
/// <param name="Slug">The id passed to the agent's model flag ('haiku', 'gpt-5.4-mini').</param>
/// <param name="DisplayName">What a person calls it ('Haiku').</param>
/// <param name="Source">Where it came from.</param>
/// <param name="Order">Its place from cheapest (0) up, as the source knows it; null when the source can't say, which puts the model last and marks it unordered.</param>
public sealed record ModelFacts(string Slug, string DisplayName, ModelSource Source, int? Order = null);
