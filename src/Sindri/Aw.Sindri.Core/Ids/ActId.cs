using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>
/// The caller's key for one agent start (agents spec §15.8): the tmux session is 'sindri-&lt;actId&gt;'. The type takes any non-empty text of at most 64 characters so that a malformed one reaches the start service,
/// which answers a plain 400 with no SND-AGENT code (8 to 32 characters of a-z and 0-9). It travels as a plain JSON string.
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<ActId>))]
public readonly partial struct ActId : ITypedId<ActId, string>
{
    private static Validation Validate(string value) =>
        value.Length is >= 1 and <= 64 ? Validation.Ok : Validation.Invalid("An act id is 1 to 64 characters");
}
