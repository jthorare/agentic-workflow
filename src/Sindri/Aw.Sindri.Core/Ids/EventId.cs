using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>An event's id (the envelope's 'id', and a 'causationId' that names an event or command), never empty.</summary>
[ValueObject<Guid>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(GuidIdJsonConverter<EventId>))]
public readonly partial struct EventId : ITypedId<EventId, Guid>
{
    private static Validation Validate(Guid value) =>
        value == Guid.Empty ? Validation.Invalid("EventId must not be empty") : Validation.Ok;
}
