using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Context.Core.Records;

/// <summary>Who may see a stored record, set at ingest (spec §4.2): 'repo:&lt;owner&gt;/&lt;repo&gt;', 'linear:&lt;teamId&gt;' or 'slack:&lt;channelId&gt;'.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<AudienceKey>))]
public readonly partial struct AudienceKey : ITypedId<AudienceKey, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("An audience key is 'repo:<owner>/<repo>', 'linear:<teamId>' or 'slack:<channelId>'");

    [GeneratedRegex("^(repo:[A-Za-z0-9][A-Za-z0-9._-]{0,99}/[A-Za-z0-9][A-Za-z0-9._-]{0,99}|(linear|slack):[A-Za-z0-9][A-Za-z0-9._-]{0,99})\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    private static string NormalizeInput(string input) => input.Length > 210 ? string.Empty : input;
}
