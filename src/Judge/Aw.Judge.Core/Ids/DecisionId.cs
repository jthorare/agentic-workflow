using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Judge.Core.Ids;

/// <summary>DecisionId: the id of one recorded judge decision (a UUID the evaluate step generates).</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<DecisionId>))]
public readonly partial struct DecisionId : ITypedId<DecisionId, string>
{
    private static Validation Validate(string value) =>
        string.IsNullOrWhiteSpace(value) ? Validation.Invalid("DecisionId must not be blank") : Validation.Ok;
}
