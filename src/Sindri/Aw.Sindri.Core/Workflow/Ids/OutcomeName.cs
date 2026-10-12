using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Workflow.Ids;

/// <summary>An outcome's name (spec §1 W6, §3.6): a camelCase word, for example 'resolved' or 'notStartable', at most 40 characters.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<OutcomeName>))]
public readonly partial struct OutcomeName : ITypedId<OutcomeName, string>
{
    private static Vogen.Validation Validate(string value) =>
        Shape.IsMatch(value) ? Vogen.Validation.Ok : Vogen.Validation.Invalid("An outcome name is a camelCase word, for example notStartable, at most 40 characters");

    [GeneratedRegex("^[a-z][A-Za-z0-9]*\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    private static string NormalizeInput(string input) => input.Length > 40 ? string.Empty : input;
}
