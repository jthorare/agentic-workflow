using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Concepts;

/// <summary>A concept a feature belongs to in a map version.</summary>
/// <param name="Id">The concept's id.</param>
/// <param name="Name">Its display name.</param>
public sealed record FeaturePlace(ConceptId Id, string Name);
