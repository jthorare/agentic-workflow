using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>A profile's approval hash: 64 lower-case hex digits of SHA-256 over its canonical JSON.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<ProfileHash>))]
public readonly partial struct ProfileHash : ITypedId<ProfileHash, string>
{
    /// <summary>The first six digits, which a human types to confirm an approval.</summary>
    public string Abbreviated => Value[..6];

    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("ProfileHash must be 64 lower-case hex digits");

    [GeneratedRegex("^[0-9a-f]{64}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
