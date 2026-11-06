using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Context.Core.Records;

/// <summary>Who may see a stored record, set at ingest (spec §4.2): 'repo:&lt;owner&gt;/&lt;repo&gt;', 'linear:&lt;teamId&gt;', 'slack:&lt;channelId&gt;' or 'owner:&lt;name&gt;' (the owner alone: the human's scope holds it, no session scope ever does and rule (c) never lets such a record through).</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<AudienceKey>))]
public readonly partial struct AudienceKey : ITypedId<AudienceKey, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("An audience key is 'repo:<owner>/<repo>', 'linear:<teamId>', 'slack:<channelId>' or 'owner:<name>'");

    [GeneratedRegex("^(repo:[A-Za-z0-9][A-Za-z0-9._-]{0,99}/[A-Za-z0-9][A-Za-z0-9._-]{0,99}|(linear|slack):[A-Za-z0-9][A-Za-z0-9._-]{0,99}|owner:[a-z][a-z-]{0,31})\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    private static string NormalizeInput(string input) => input.Length > 210 ? string.Empty : input;
}
