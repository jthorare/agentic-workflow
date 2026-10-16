using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>A needs-you row's id (API spec §3.6): 'decision:DEC-…', 'failure:service:&lt;name&gt;' or 'profile:drift'. Stable while the row lives, so the UI keys on it.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<NeedsYouRowId>))]
public readonly partial struct NeedsYouRowId : ITypedId<NeedsYouRowId, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A needs-you row id is 'decision:DEC-…', 'failure:service:<name>' or 'profile:drift'");

    [GeneratedRegex("^(?:decision:DEC-[0-9a-f]{32}|failure:service:[A-Za-z0-9][A-Za-z0-9._-]{0,99}|profile:drift)\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
