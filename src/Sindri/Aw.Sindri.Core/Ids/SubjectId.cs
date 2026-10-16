using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>The id of a needs-you row's subject, as text ('DEC-…', a service name, the live profile hash): letters, digits, '.', '_', ':' and '-', so it is safe on a command line.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<SubjectId>))]
public readonly partial struct SubjectId : ITypedId<SubjectId, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A subject id is 1 to 200 letters, digits, '.', '_', ':' or '-', starting with a letter or digit");

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._:-]{0,199}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
