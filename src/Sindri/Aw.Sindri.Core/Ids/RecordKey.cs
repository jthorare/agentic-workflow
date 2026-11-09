using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>
/// A tracker record an item is linked to: '&lt;source&gt;:&lt;kind&gt;/&lt;id&gt;', for example 'linear:issue/&lt;uuid&gt;'. Source and
/// kind are lower-case kebab words of at most 20 characters; the id is 1 to 64 letters, digits, hyphens, '@' or ':' (a workflow run id is 'nightly@1:1').
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<RecordKey>))]
public readonly partial struct RecordKey : ITypedId<RecordKey, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A record key is '<source>:<kind>/<id>', for example 'linear:issue/abc-123'");

    [GeneratedRegex("^[a-z][a-z0-9-]{0,19}:[a-z][a-z0-9-]{0,19}/[A-Za-z0-9@:-]{1,64}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
