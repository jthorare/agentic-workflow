using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Judge.Core.Ids;

/// <summary>PromptId: the provider's id for the prompt that dispatched a subagent; any string the hook passes, as the TS judge accepts.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<PromptId>))]
public readonly partial struct PromptId : ITypedId<PromptId, string>
{
    // Any string the hook passes is accepted, blank included, as the TS judge accepts it (frozen hook surface);
    // the cap only keeps a runaway argument out of the store.
    private static Validation Validate(string value) =>
        value.Length <= 4096 ? Validation.Ok : Validation.Invalid("PromptId must be at most 4096 characters");
}
