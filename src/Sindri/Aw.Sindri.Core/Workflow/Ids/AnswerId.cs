using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Workflow.Ids;

/// <summary>The id of the question a 'waitFor: { answer }' state asks (spec §3.4): a camelCase or kebab-case word, at most 40 characters.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<AnswerId>))]
public readonly partial struct AnswerId : ITypedId<AnswerId, string>
{
    private static Vogen.Validation Validate(string value) =>
        Shape.IsMatch(value) ? Vogen.Validation.Ok : Vogen.Validation.Invalid("An answer id is a camelCase or kebab-case word, at most 40 characters");

    [GeneratedRegex("^[a-z][A-Za-z0-9]*(-[a-z0-9]+)*\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    private static string NormalizeInput(string input) => input.Length > 40 ? string.Empty : input;
}
