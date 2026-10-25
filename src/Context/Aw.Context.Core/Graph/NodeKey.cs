using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Context.Core.Graph;

/// <summary>
/// A product-graph node's stable key (spec §6.1): its kind as the scheme and a name, such as
/// 'feature:layout.split-pane.orientation', 'surface:command/split-vertical' or 'code:src/layout/SplitPane.ts'.
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<NodeKey>))]
public readonly partial struct NodeKey : ITypedId<NodeKey, string>
{
    /// <summary>The node's kind, read from the key's scheme.</summary>
    public NodeKind Kind => NodeKinds.FromScheme(Value[..Value.IndexOf(':', StringComparison.Ordinal)]);

    /// <summary>The part after the scheme.</summary>
    public string Name => Value[(Value.IndexOf(':', StringComparison.Ordinal) + 1)..];

    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid(
            "A node key is a kind (feature, surface, action, thing, flow, decision, record, concept or code), a colon and 1 to 1024 characters with no control or format characters");

    [GeneratedRegex("^(feature|surface|action|thing|flow|decision|record|concept|code):[^\\p{C}\\s][^\\p{C}]{0,1023}\\z",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    private static string NormalizeInput(string input) => input.Length > 1100 ? string.Empty : input;
}
