using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Reviews;

/// <summary>Writes a panel with its rows under <c language="text">data</c> as the panel's own type, and reads them back by the panel's id (the wire has no type tag).</summary>
public sealed class ReviewPagePanelConverter : JsonConverter<ReviewPagePanel>
{
    /// <inheritdoc />
    public override ReviewPagePanel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var id = root.GetProperty("id").GetString() ?? string.Empty;
        var text = root.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
        var data = root.GetProperty("data");
        var type = DataType(id);
        var rows = (ReviewPanelData)(data.Deserialize(options.GetTypeInfo(type)) ?? throw new JsonException("a panel without rows"));
        return new ReviewPagePanel(id, root.GetProperty("state").GetString() ?? string.Empty, text, rows);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, ReviewPagePanel value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);
        writer.WriteStartObject();
        writer.WriteString("id", value.Key);
        writer.WriteString("state", value.State);
        if (value.Text is null)
        {
            writer.WriteNull("text");
        }
        else
        {
            writer.WriteString("text", value.Text);
        }

        writer.WritePropertyName("data");
        JsonSerializer.Serialize(writer, value.Data, options.GetTypeInfo(value.Data.GetType()));
        writer.WriteEndObject();
    }

    private static Type DataType(string id) => id switch
    {
        "requiredChecks" => typeof(RequiredChecksData),
        "docMap" => typeof(DocMapData),
        "testPlan" => typeof(TestPlanData),
        "ci" => typeof(CiData),
        "evidence" => typeof(EvidenceData),
        "risk" => typeof(RiskData),
        "conversation" => typeof(ConversationData),
        _ => throw new JsonException("an unknown panel: " + id),
    };
}
