using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Context.Core.Sources;

/// <summary>A source adapter's id, such as 'repo-docs', 'linear' or 'slack': kebab-case, at most 40 characters.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<SourceId>))]
public readonly partial struct SourceId : ITypedId<SourceId, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A source id is kebab-case, 1 to 40 characters");

    [GeneratedRegex("^[a-z][a-z0-9]*(-[a-z0-9]+)*\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    private static string NormalizeInput(string input) => input.Length > 40 ? string.Empty : input;
}
