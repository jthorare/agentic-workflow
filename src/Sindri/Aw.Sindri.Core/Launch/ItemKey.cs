using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Launch;

/// <summary>
/// The item an instance works on (spec §5.1), as the engine names it: API P3's 'ITM-…' id, or a tracker key such as
/// 'T-3' in evals and signals. An upper-case prefix, a hyphen and letters or digits, at most 64 characters. API P3's
/// ItemId replaces it when that phase lands.
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<ItemKey>))]
public readonly partial struct ItemKey : ITypedId<ItemKey, string>
{
    private static Vogen.Validation Validate(string value) =>
        Shape.IsMatch(value) ? Vogen.Validation.Ok : Vogen.Validation.Invalid("An item is an upper-case prefix, a hyphen and letters or digits ('ITM-0192…', 'T-3'), at most 64 characters");

    [GeneratedRegex("^[A-Z][A-Z0-9]*-[A-Za-z0-9]+\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    private static string NormalizeInput(string input) => input.Length > 64 ? string.Empty : input;
}
