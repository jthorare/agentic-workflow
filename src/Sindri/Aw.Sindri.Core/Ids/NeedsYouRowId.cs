using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>A needs-you row's id (API spec §3.6): 'decision:DEC-…', 'park:WFI-…' (also 'desk:' and 'takeover:'), 'failure:service:&lt;name&gt;', 'profile:drift', 'setup:repo|recipes|mcp' 'recipe:workflow.&lt;name&gt;', 'connection:…', 'workflow-notify:&lt;run&gt;:&lt;node&gt;' or 'question:&lt;run&gt;:&lt;node&gt;[:&lt;item&gt;]' (the item, counted from 1, for an 'ask_owner' node in a 'for_each' body). Stable while the row lives, so the UI keys on it.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<NeedsYouRowId>))]
public readonly partial struct NeedsYouRowId : ITypedId<NeedsYouRowId, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A needs-you row id is 'decision:DEC-…', 'park:WFI-…' (or 'desk:', 'takeover:'), 'failure:service:<name>', 'profile:drift', 'setup:repo', 'setup:recipes' or 'setup:mcp', 'recipe:workflow.<name>', 'connection:<service>:<label>', 'workflow-notify:<run>:<node>' or 'question:<run>:<node>[:<item>]'");

    [GeneratedRegex("^(?:decision:DEC-[0-9a-f]{32}|(?:park|desk|takeover):WFI-[0-9a-f]{32}|failure:service:[A-Za-z0-9][A-Za-z0-9._-]{0,99}|profile:drift|setup:(?:repo|recipes|mcp)|recipe:workflow\\.[a-z][a-z0-9]*(?:-[a-z0-9]+)*|connection:[a-z0-9][a-z0-9-]*:[a-z0-9][a-z0-9-]*|workflow-notify:[a-z0-9][a-z0-9-]{0,62}@[1-9][0-9]{0,8}:[1-9][0-9]{0,8}:[a-z][a-z0-9_]{0,63}|question:[a-z0-9][a-z0-9-]{0,62}@[1-9][0-9]{0,8}:[1-9][0-9]{0,8}:[a-z][a-z0-9_]{0,63}(?::[1-9][0-9]{0,8})?)\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
