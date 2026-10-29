using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>
/// An owner project's id (projects page spec §5.1): 'prj_' and a 26-character ULID in Crockford base32 (uppercase). The ULID is the
/// id port's GUID (v7 in production, so ids sort by creation) written as 128 bits in 26 five-bit digits.
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<ProjectId>))]
public readonly partial struct ProjectId : ITypedId<ProjectId, string>
{
    private const string Prefix = "prj_";
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int Digits = 26;

    /// <summary>A new id from the id port.</summary>
    /// <param name="ids">The id port (a GUIDv7 in production, a sequence in tests).</param>
    /// <returns>The id.</returns>
    public static ProjectId New(IIdGenerator ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var bytes = ids.NewId().ToByteArray(bigEndian: true);
        var value = UInt128.Zero;
        foreach (var b in bytes)
        {
            value = (value << 8) | b;
        }

        var text = new char[Digits];
        for (var i = Digits - 1; i >= 0; i--)
        {
            text[i] = Alphabet[(int)(value & 31)];
            value >>= 5;
        }

        return From(Prefix + new string(text));
    }

    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A project id is 'prj_' and 26 uppercase Crockford base32 digits");

    [GeneratedRegex("^prj_[0-7][0-9A-HJKMNP-TV-Z]{25}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
