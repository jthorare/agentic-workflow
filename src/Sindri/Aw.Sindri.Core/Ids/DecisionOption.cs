using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>An option of a decision ('accept', 'decline'): a lower-case kebab word, safe on a command line (CliForm.Fill).</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<DecisionOption>))]
public readonly partial struct DecisionOption : ITypedId<DecisionOption, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A decision option is 1 to 32 lower-case letters and '-', starting with a letter");

    [GeneratedRegex("^[a-z][a-z-]{0,31}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
