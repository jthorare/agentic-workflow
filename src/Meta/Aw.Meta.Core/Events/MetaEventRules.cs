using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aw.Kernel;

namespace Aw.Meta.Core.Events;

/// <summary>What makes an event well-formed: a dotted lower-case kind and its fields as one JSON object. Any well-formed kind is accepted, so a reader sees kinds a later writer adds.</summary>
public static partial class MetaEventRules
{
    /// <summary>Whether <paramref name="kind"/> is dotted lower-case with at least two parts (obligation.opened, heavy.wait.long); digits and underscores are allowed after the first letter of a part.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>True when it is well-formed.</returns>
    public static bool IsKind(string? kind) => kind is { Length: > 0 } && KindPattern.IsMatch(kind);

    /// <summary>Checks an event and returns it with its fields as compact JSON, so every store holds the same text.</summary>
    /// <param name="metaEvent">The event to append.</param>
    /// <returns>The event, or AW-META-027.</returns>
    public static Result<MetaEvent> Check(MetaEvent metaEvent)
    {
        ArgumentNullException.ThrowIfNull(metaEvent);
        if (!IsKind(metaEvent.Kind))
        {
            return MetaErrors.EventInvalid.With("the kind '" + metaEvent.Kind + "' is not dotted lower-case (such as obligation.opened)");
        }

        var fields = Compact(metaEvent.Fields);
        return fields is null
            ? MetaErrors.EventInvalid.With("the fields of " + metaEvent.Kind + " are not one JSON object")
            : metaEvent with { Fields = fields };
    }

    private static string? Compact(string? fields)
    {
        if (string.IsNullOrWhiteSpace(fields))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(fields);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
            {
                document.RootElement.WriteTo(writer);
            }

            return Encoding.UTF8.GetString(buffer.WrittenSpan);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex KindPattern { get; }
}
