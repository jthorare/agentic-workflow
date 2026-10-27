namespace Aw.Context.Core.Concepts;

/// <summary>A model's name for one derived concept.</summary>
/// <param name="Name">The capability a user would name ('Self scheduling').</param>
/// <param name="Summary">One sentence, or null.</param>
public sealed record ConceptName(string Name, string? Summary = null);
