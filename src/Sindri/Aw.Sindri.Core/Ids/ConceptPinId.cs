using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>The id an 'index.concepts' entry pins: the slug of the concept's key ('scheduling' pins concept:scheduling).</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<ConceptPinId>))]
public readonly partial struct ConceptPinId : ITypedId<ConceptPinId, string>
{
    private static Validation Validate(string value) =>
        value.Length is > 0 and <= 64 && !value.Any(char.IsControl) ? Validation.Ok : Validation.Invalid("A concept id is 1 to 64 characters; the profile's rules check its shape");

    private static string NormalizeInput(string input) => input.Length > 64 ? string.Empty : input;
}
