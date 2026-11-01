using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>The id of an action of an 'index.pages' entry, unique within its page; left out, the name gives one.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<PageActionPinId>))]
public readonly partial struct PageActionPinId : ITypedId<PageActionPinId, string>
{
    private static Validation Validate(string value) =>
        value.Length is > 0 and <= 64 && !value.Any(char.IsControl) ? Validation.Ok : Validation.Invalid("An id is 1 to 64 characters; the profile's rules check its shape");

    private static string NormalizeInput(string input) => input.Length > 64 ? string.Empty : input;
}
