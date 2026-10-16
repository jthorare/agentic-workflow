using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Requests;

/// <summary>
/// Reads a request kind by its name, as <see cref="RequestKind"/>'s member names say. A name that isn't a kind reads as an
/// undefined value, so <see cref="RequestRules.Validate"/> answers SND-REQ-001 (API spec §9) instead of the body failing to
/// bind (SND-CLI-003); anything but a string is still a binding failure.
/// </summary>
public sealed class RequestKindConverter : JsonConverter<RequestKind>
{
    /// <summary>The value an unknown name reads as; not a member of <see cref="RequestKind"/>.</summary>
    internal const RequestKind Unknown = (RequestKind)(-1);

    /// <inheritdoc />
    public override RequestKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("A request kind is a name: rescope, add-task, note or handback.");
        }

        return reader.GetString() switch
        {
            "rescope" => RequestKind.Rescope,
            "add-task" => RequestKind.AddTask,
            "note" => RequestKind.Note,
            "handback" => RequestKind.Handback,
            _ => Unknown,
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, RequestKind value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value switch
        {
            RequestKind.Rescope => "rescope",
            RequestKind.AddTask => "add-task",
            RequestKind.Note => "note",
            RequestKind.Handback => "handback",
            _ => throw new JsonException("Not a request kind."),
        });
    }
}
