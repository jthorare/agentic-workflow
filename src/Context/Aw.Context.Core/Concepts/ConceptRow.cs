using System.Text.Json.Serialization;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Concepts;

/// <summary>One concept of the top band.</summary>
/// <param name="Id">The concept's key ('concept:scheduling').</param>
/// <param name="Name">Its name as the caller reads it.</param>
/// <param name="Summary">One sentence, or empty when it has none.</param>
/// <param name="Source">'derived' or 'override'.</param>
/// <param name="Size">What it holds.</param>
/// <param name="Citations">The records it rests on.</param>
public sealed record ConceptRow(NodeKey Id, string Name, string Summary, string Source, ConceptSize Size, IReadOnlyList<ConceptCitation> Citations)
{
    /// <summary>The words that characterize the concept, the most telling first; null (left out of the answer) when the map was built without a glossary.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Terms { get; init; }

    /// <summary>True for a concept the owner pinned in the profile (<see cref="Source"/> says 'override' for it too, so this is how an agent finds a pin id); left out when false.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Pinned { get; init; }

    /// <summary>The owner's other words for a pin; null (left out of the answer) when it has none.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Aliases { get; init; }
}
