using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>The machine a process runs on (its host name), as the lease records it.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<HostId>))]
public readonly partial struct HostId : ITypedId<HostId, string>
{
    private static Validation Validate(string value) =>
        value.Length is > 0 and <= 255 && !value.Any(char.IsWhiteSpace)
            ? Validation.Ok
            : Validation.Invalid("A host id is 1 to 255 characters with no spaces");
}
