using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Aw.Sindri.Core.Profile;

/// <summary>Reads and writes the 'agents:' block: 'softLimit' beside the agent ids, entries in ordinal id order so the canonical JSON is stable.</summary>
public sealed class AgentSettingsJsonConverter : JsonConverter<AgentSettings>
{
    /// <inheritdoc />
    public override AgentSettings Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("agents must be a mapping");
        }

        var limits = (JsonTypeInfo<AgentLimits>)options.GetTypeInfo(typeof(AgentLimits));
        decimal? soft = null;
        var targets = new Dictionary<string, AgentLimits>(StringComparer.Ordinal);
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var name = reader.GetString()!;   // why: a property name token always has text
            _ = reader.Read();
            if (string.Equals(name, AgentSettings.SoftLimitKey, StringComparison.Ordinal))
            {
                soft = reader.TokenType == JsonTokenType.Number && reader.TryGetDecimal(out var parsed)
                    ? parsed
                    : throw new JsonException("agents.softLimit must be a number");
            }
            else
            {
                targets[name] = JsonSerializer.Deserialize(ref reader, limits) ?? new AgentLimits();
            }
        }

        return new AgentSettings(soft, targets.Count == 0 ? null : targets);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, AgentSettings value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);
        var limits = (JsonTypeInfo<AgentLimits>)options.GetTypeInfo(typeof(AgentLimits));
        writer.WriteStartObject();
        if (value.SoftLimit is { } soft)
        {
            writer.WriteNumber(AgentSettings.SoftLimitKey, soft);
        }

        foreach (var (name, entry) in (value.Targets ?? new Dictionary<string, AgentLimits>(StringComparer.Ordinal)).OrderBy(static t => t.Key, StringComparer.Ordinal))
        {
            writer.WritePropertyName(name);
            JsonSerializer.Serialize(writer, entry, limits);
        }

        writer.WriteEndObject();
    }
}
