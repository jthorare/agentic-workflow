using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>
/// A block's 'maxDuration' as the descriptor writes it: whole hours ('1h'), minutes ('2m') or seconds ('90s'), whichever is the
/// coarsest that is exact. AOT-safe (no reflection); a nullable property uses it for its non-null value.
/// </summary>
public sealed class BlockDurationJsonConverter : JsonConverter<TimeSpan>
{
    /// <inheritdoc/>
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString() ?? string.Empty;
        if (text.Length > 1 && long.TryParse(text.AsSpan(0, text.Length - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var n))
        {
            switch (text[^1])
            {
                case 's':
                    return TimeSpan.FromSeconds(n);
                case 'm':
                    return TimeSpan.FromMinutes(n);
                case 'h':
                    return TimeSpan.FromHours(n);
                default:
                    break;
            }
        }

        throw new JsonException($"'{text}' isn't a duration (use '90s', '2m' or '1h')");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        var seconds = (long)value.TotalSeconds;
        writer.WriteStringValue(seconds % 3600 == 0 && seconds > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{seconds / 3600}h")
            : seconds % 60 == 0 && seconds > 0 ? string.Create(CultureInfo.InvariantCulture, $"{seconds / 60}m") : string.Create(CultureInfo.InvariantCulture, $"{seconds}s"));
    }
}
