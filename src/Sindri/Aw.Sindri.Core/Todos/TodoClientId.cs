using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Todos;

/// <summary>A caller's retry id for adding a todo (todo spec §4.1): 1 to 64 of 'A-Za-z0-9_-'. A retried add with the same id makes one todo.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<TodoClientId>))]
public readonly partial struct TodoClientId : ITypedId<TodoClientId, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A client id is 1 to 64 of A-Z, a-z, 0-9, '_' and '-'");

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
