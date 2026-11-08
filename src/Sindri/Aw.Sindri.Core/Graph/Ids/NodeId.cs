using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Graph.Ids;

/// <summary>A node's id inside one workflow, the name references use ('{{agent.drifted}}'): a lowercase letter, then lowercase letters, digits and '_', at most 64.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<NodeId>))]
public readonly partial struct NodeId : ITypedId<NodeId, string>
{
    private static Vogen.Validation Validate(string value) =>
        Shape.IsMatch(value) ? Vogen.Validation.Ok : Vogen.Validation.Invalid("A node id is a lowercase letter, then lowercase letters, digits and '_', at most 64");

    [GeneratedRegex("^[a-z][a-z0-9_]{0,63}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
