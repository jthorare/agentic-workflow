using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Workflow.Ids;

/// <summary>
/// A block descriptor's hash (spec §2): 'sha256:' and 64 lower-case hex digits of SHA-256 over the RFC 8785 canonical
/// JSON of the descriptor. An approval pins one per block the recipe names (W11).
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<DescriptorHash>))]
public readonly partial struct DescriptorHash : ITypedId<DescriptorHash, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A descriptor hash is 'sha256:' and 64 lower-case hex digits");

    [GeneratedRegex("^sha256:[0-9a-f]{64}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
