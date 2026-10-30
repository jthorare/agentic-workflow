namespace Aw.Sindri.Core.Setup;

/// <summary>What <see cref="ConceptList.Parse"/> read.</summary>
/// <param name="Concepts">The concepts, in order, at most <see cref="ConceptList.Max"/>.</param>
/// <param name="Skipped">How many further concepts were left out because the list was longer than <see cref="ConceptList.Max"/>.</param>
public sealed record ConceptListRead(IReadOnlyList<ConceptLine> Concepts, int Skipped);
