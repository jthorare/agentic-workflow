namespace Aw.Context.Core.Concepts;

/// <summary>What a concepts answer says about the map it read.</summary>
/// <param name="MapVersion">The version ('v13'), or empty when no map was read.</param>
/// <param name="Repo">The repo the answer is for, or null.</param>
public sealed record ConceptsBasis(string MapVersion, string? Repo);
