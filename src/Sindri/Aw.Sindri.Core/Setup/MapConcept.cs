namespace Aw.Sindri.Core.Setup;

/// <summary>A concept of the current map, for resolving a name in a page action.</summary>
/// <param name="Key">The concept's id, without the 'concept:' prefix.</param>
/// <param name="Name">What the map shows.</param>
public sealed record MapConcept(string Key, string Name);
