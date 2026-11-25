using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>An Agents page row's id (agents spec §3.1, §15.8): 'sindri-&lt;actId&gt;' for a one-off session that has not written its fleet file yet, 'session:&lt;provider&gt;/&lt;id&gt;' or 'lane:&lt;team&gt;/&lt;lane&gt;'. It travels as a plain JSON string.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<AgentRowId>))]
public readonly partial struct AgentRowId : ITypedId<AgentRowId, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("An agent row id is 'sindri-<actId>' (8 to 32 characters of a-z and 0-9), 'session:<provider>/<id>' or 'lane:<team>/<lane>'");

    [GeneratedRegex("^(?:sindri-[a-z0-9]{8,32}|(?:session|lane):[A-Za-z0-9][A-Za-z0-9._@:/-]{0,127})\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
