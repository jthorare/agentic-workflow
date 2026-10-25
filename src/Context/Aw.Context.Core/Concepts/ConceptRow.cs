using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Concepts;

/// <summary>One concept of the top band.</summary>
/// <param name="Id">The concept's key ('concept:scheduling').</param>
/// <param name="Name">Its name as the caller reads it.</param>
/// <param name="Summary">One sentence, or empty when it has none.</param>
/// <param name="Source">'derived' or 'override'.</param>
/// <param name="Size">What it holds.</param>
/// <param name="Citations">The records it rests on.</param>
public sealed record ConceptRow(NodeKey Id, string Name, string Summary, string Source, ConceptSize Size, IReadOnlyList<ConceptCitation> Citations);
