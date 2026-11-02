using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Judge.Core.Ids;

/// <summary>EvalItemId: the id of one eval item ('item-' plus the decision's id for an item made from a decision, a UUID for a transcript turn).</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<EvalItemId>))]
public readonly partial struct EvalItemId : ITypedId<EvalItemId, string>
{
    private static Validation Validate(string value) =>
        string.IsNullOrWhiteSpace(value) ? Validation.Invalid("EvalItemId must not be blank") : Validation.Ok;
}
