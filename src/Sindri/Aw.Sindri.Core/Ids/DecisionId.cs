using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>A decision's id (API spec §3.2 'Decide'): 'DEC-' and 32 lowercase hex digits, a GUIDv7 in 'N' format.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<DecisionId>))]
public readonly partial struct DecisionId : ITypedId<DecisionId, string>
{
    private const string Prefix = "DEC-";

    /// <summary>A new id from the id port.</summary>
    /// <param name="ids">The id port (a GUIDv7 in production, a sequence in tests).</param>
    /// <returns>The id.</returns>
    public static DecisionId New(IIdGenerator ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return From(Prefix + ids.NewId().ToString("N", CultureInfo.InvariantCulture));
    }

    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A decision id is 'DEC-' and 32 lowercase hex digits");

    [GeneratedRegex("^DEC-[0-9a-f]{32}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
