using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Context.Core.Engine;

/// <summary>A concept's id, the slug of its key without the 'concept:' prefix ('scheduling' for concept:scheduling).</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<ConceptId>))]
public readonly partial struct ConceptId : ITypedId<ConceptId, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A concept id is 1 to 64 lower-case letters, digits, '.' or '-', starting with a letter or digit (like 'scheduling' or 'tabs.tab-manager')");

    [GeneratedRegex("^[a-z0-9][a-z0-9.-]{0,63}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    private static string NormalizeInput(string input) => input.Length > 64 ? string.Empty : input;
}
