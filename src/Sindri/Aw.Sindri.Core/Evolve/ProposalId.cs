using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Evolve;

/// <summary>A proposal's id (issue 3787): 'prp_' and 32 lowercase hex digits, a GUIDv7 in 'N' format.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<ProposalId>))]
public readonly partial struct ProposalId : ITypedId<ProposalId, string>
{
    private const string Prefix = "prp_";

    /// <summary>A new id from the id port.</summary>
    /// <param name="ids">The id port (a GUIDv7 in production, a sequence in tests).</param>
    /// <returns>The id.</returns>
    public static ProposalId New(IIdGenerator ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return From(Prefix + ids.NewId().ToString("N", CultureInfo.InvariantCulture));
    }

    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A proposal id is 'prp_' and 32 lowercase hex digits");

    [GeneratedRegex("^prp_[0-9a-f]{32}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
