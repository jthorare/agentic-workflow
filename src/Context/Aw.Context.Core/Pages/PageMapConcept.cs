using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Pages;

/// <summary>One concept of the map, as a page write resolves a name to it.</summary>
/// <param name="Id">Its id, without the 'concept:' prefix.</param>
/// <param name="Name">The name the map shows.</param>
public sealed record PageMapConcept(ConceptId Id, string Name);
