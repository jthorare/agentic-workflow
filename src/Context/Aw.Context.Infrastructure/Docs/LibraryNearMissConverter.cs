using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Aw.Context.Core.Library;

namespace Aw.Context.Infrastructure.Docs;

/// <summary>
/// Reads a near miss of the library status file in either shape: the finding (path and SND-LIB-019 error), or the bare root-relative path a status file written before findings holds. A bare path reads as a 019 with no line,
/// "a near miss (read again on the next pass)" and no fix, until the next pass writes the finding. Writes the finding.
/// </summary>
internal sealed class LibraryNearMissConverter : JsonConverter<DesignDocFinding>
{
    /// <summary>The message of a near miss read from a bare path.</summary>
    internal const string LegacyMessage = "a near miss (read again on the next pass)";

    public override DesignDocFinding Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new DesignDocFinding(reader.GetString() ?? string.Empty, new DesignDocError(DesignDocCodes.NearMiss, Line: null, LegacyMessage, Fix: string.Empty));
        }

        var stored = JsonSerializer.Deserialize(ref reader, Stored(options)) ?? throw new JsonException("a near miss is null");
        return new DesignDocFinding(stored.Path, stored.Error);
    }

    public override void Write(Utf8JsonWriter writer, DesignDocFinding value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        JsonSerializer.Serialize(writer, new StoredNearMiss(value.Path, value.Error), Stored(options));
    }

    private static JsonTypeInfo<StoredNearMiss> Stored(JsonSerializerOptions options) => (JsonTypeInfo<StoredNearMiss>)options.GetTypeInfo(typeof(StoredNearMiss));
}
