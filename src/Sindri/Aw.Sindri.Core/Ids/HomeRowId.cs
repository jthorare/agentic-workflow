using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>
/// A Home row's id (home spec §3.1): a lowercase source name, a colon and the source's own id, such as 'gh:pr:jthorare/agentic-workflow#3120',
/// 'session:claude-code/ab12cd34', 'lane:meta/ideas' or 'linear:issue:ENG-412'. A needs-you row keeps its needs-you id as the source id. Stable while the row lives.
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<HomeRowId>))]
public readonly partial struct HomeRowId : ITypedId<HomeRowId, string>
{
    /// <summary>The id as one URL path segment: every character that is not unreserved is percent-encoded, so ':' '/' and '#' survive a route.</summary>
    /// <returns>The encoded segment.</returns>
    public string PathSegment() => Uri.EscapeDataString(Value);

    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A Home row id is a lowercase source, a colon and the source's id, up to 300 characters with no space or control character, such as 'gh:pr:owner/repo#12'");

    [GeneratedRegex("^[a-z][a-z0-9-]{0,31}:[^\\s\\p{C}]{1,260}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
