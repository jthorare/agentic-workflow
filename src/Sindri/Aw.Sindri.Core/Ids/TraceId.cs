using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>The trace an event was recorded under (the envelope's 'traceId'): 1 to 128 printable ASCII characters, no spaces.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<TraceId>))]
public readonly partial struct TraceId : ITypedId<TraceId, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A trace id is 1 to 128 printable ASCII characters with no spaces");

    [GeneratedRegex("^[!-~]{1,128}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
