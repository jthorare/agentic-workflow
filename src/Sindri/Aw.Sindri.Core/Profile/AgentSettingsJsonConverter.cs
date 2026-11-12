using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Aw.Sindri.Core.Profile;

/// <summary>
/// Reads and writes the 'agents:' block: 'softLimit', 'idleOwedMinutes', 'slotWaitMinutes' and 'page' beside the agent ids, entries in ordinal id order so the canonical JSON is stable.
/// </summary>
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
        var keys = new ReservedKeys();
        var targets = new Dictionary<string, AgentLimits>(StringComparer.Ordinal);
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var name = reader.GetString()!;   // why: a property name token always has text
            _ = reader.Read();
            if (!keys.TryRead(ref reader, name))
            {
                targets[name] = JsonSerializer.Deserialize(ref reader, limits) ?? new AgentLimits();
            }
        }

        return new AgentSettings(keys.SoftLimit, targets.Count == 0 ? null : targets, keys.IdleOwedMinutes, keys.SlotWaitMinutes, keys.Page);
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

        WriteWhole(writer, AgentSettings.IdleOwedMinutesKey, value.IdleOwedMinutes);
        WriteWhole(writer, AgentSettings.SlotWaitMinutesKey, value.SlotWaitMinutes);
        if (value.Page is { } page)
        {
            writer.WriteString(AgentSettings.PageKey, page);
        }

        foreach (var (name, entry) in (value.Targets ?? new Dictionary<string, AgentLimits>(StringComparer.Ordinal)).OrderBy(static t => t.Key, StringComparer.Ordinal))
        {
            writer.WritePropertyName(name);
            JsonSerializer.Serialize(writer, entry, limits);
        }

        writer.WriteEndObject();
    }

    private static void WriteWhole(Utf8JsonWriter writer, string key, int? value)
    {
        if (value is { } number)
        {
            writer.WriteNumber(key, number);
        }
    }

    /// <summary>The keys of the block that are not agent ids, collected while the block is read.</summary>
    private sealed class ReservedKeys
    {
        public decimal? SoftLimit { get; private set; }

        public int? IdleOwedMinutes { get; private set; }

        public int? SlotWaitMinutes { get; private set; }

        public string? Page { get; private set; }

        /// <summary>Reads the value at <paramref name="reader"/> when <paramref name="name"/> is a reserved key; false (the reader untouched) for an agent id.</summary>
        public bool TryRead(ref Utf8JsonReader reader, string name)
        {
            switch (name)
            {
                case AgentSettings.SoftLimitKey:
                    SoftLimit = reader.TokenType == JsonTokenType.Number && reader.TryGetDecimal(out var soft) ? soft : throw new JsonException("agents.softLimit must be a number");
                    return true;
                case AgentSettings.IdleOwedMinutesKey:
                    IdleOwedMinutes = Whole(ref reader, name);
                    return true;
                case AgentSettings.SlotWaitMinutesKey:
                    SlotWaitMinutes = Whole(ref reader, name);
                    return true;
                case AgentSettings.PageKey:
                    Page = reader.TokenType == JsonTokenType.String ? reader.GetString() : throw new JsonException("agents.page must be text");
                    return true;
                default:
                    return false;
            }
        }

        private static int Whole(ref Utf8JsonReader reader, string key) =>
            reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var number) ? number : throw new JsonException($"agents.{key} must be a whole number");
    }
}
