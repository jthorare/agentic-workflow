using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Judge.Core.Ids;

/// <summary>SessionId: the provider's session id; any string the hook passes, as the TS judge accepts.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<SessionId>))]
public readonly partial struct SessionId : ITypedId<SessionId, string>
{
    // Any string the hook passes is accepted, blank included, as the TS judge accepts it (frozen hook surface);
    // the cap only keeps a runaway argument out of the store.
    private static Validation Validate(string value) =>
        value.Length <= 4096 ? Validation.Ok : Validation.Invalid("SessionId must be at most 4096 characters");
}
