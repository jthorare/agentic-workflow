using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Pages;

/// <summary>One concept written after '->', and the concept it resolved to.</summary>
/// <param name="Written">The text as pasted.</param>
/// <param name="Id">The concept id, or null when no pin or map concept has it.</param>
public sealed record PageListConcept(string Written, ConceptId? Id);
