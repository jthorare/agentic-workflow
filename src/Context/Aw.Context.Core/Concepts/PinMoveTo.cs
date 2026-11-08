using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Concepts;

/// <summary>One place lost features went.</summary>
/// <param name="Id">The concept that took them, or null when they are in no concept.</param>
/// <param name="Name">That concept's display name, or null with a null <paramref name="Id"/>.</param>
/// <param name="Features">How many of the pin's lost features went there.</param>
public sealed record PinMoveTo(ConceptId? Id, string? Name, int Features);
