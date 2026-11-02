using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Context.Core.Sources;

/// <summary>A record's id inside its source (a repo doc chunk 'repo/path#L3-L20', a Linear issue id): 1 to 1024 printable characters.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<ExternalId>))]
public readonly partial struct ExternalId : ITypedId<ExternalId, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("An external id is 1 to 1024 characters with no control or format characters");

    [GeneratedRegex("^[^\\p{C}]{1,1024}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    private static string NormalizeInput(string input) => input.Length > 1024 ? string.Empty : input;
}
