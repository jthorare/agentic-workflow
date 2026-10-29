using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>A model call of the context engine: the idempotency key of its 'context.model.called' event.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<ModelCallId>))]
public readonly partial struct ModelCallId : ITypedId<ModelCallId, string>
{
    private static Validation Validate(string value) =>
        value.Length is > 0 and <= 64 && !value.Any(char.IsControl) ? Validation.Ok : Validation.Invalid("A model call id is 1 to 64 characters with no control characters");

    private static string NormalizeInput(string input) => input.Length > 64 ? string.Empty : input;
}
