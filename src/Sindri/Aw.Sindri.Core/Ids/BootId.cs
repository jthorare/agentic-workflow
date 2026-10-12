using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>A boot session of a machine (macOS kern.bootsessionuuid, Linux boot_id): a pid is only comparable within one.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<BootId>))]
public readonly partial struct BootId : ITypedId<BootId, string>
{
    private static Validation Validate(string value) =>
        value.Length is > 0 and <= 128 && !value.Any(char.IsWhiteSpace)
            ? Validation.Ok
            : Validation.Invalid("A boot id is 1 to 128 characters with no spaces");
}
