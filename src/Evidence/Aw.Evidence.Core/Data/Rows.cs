using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Aw.Evidence.Core.Data;

/// <summary>What a query returned: its column names, at most the row limit of rows, and whether more were left.</summary>
/// <param name="Columns">The column names, in order.</param>
/// <param name="Values">The rows; each holds one value per column (null, bool, a number, string, bytes, a date or time, or an array).</param>
/// <param name="Truncated">True when the row limit cut the result.</param>
/// <param name="SourceColumns">For each column, the name of the table column it reads when the engine can say (null for a computed one); null when the engine can't say at all.</param>
public sealed record Rows(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<object?>> Values, bool Truncated, IReadOnlyList<string?>? SourceColumns = null)
{
    /// <summary>The number of rows kept.</summary>
    public int Count => Values.Count;

    /// <summary>The key each column has in <see cref="ToJson"/>: its name, or the name with a number after it when the result repeats the name.</summary>
    public IReadOnlyList<string> Keys => UniqueKeys(Columns);

    /// <summary>The rows as a JSON array of objects keyed by column name. A name a result repeats (a join's two id, Postgres's ?column?) keeps its first place and the later ones become name_2, name_3, skipping any name the result really has, so every key is unique and a pointer can reach every column.</summary>
    /// <returns>The JSON text.</returns>
    public string ToJson()
    {
        var keys = UniqueKeys(Columns);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var row in Values)
            {
                writer.WriteStartObject();
                for (var i = 0; i < Columns.Count; i++)
                {
                    writer.WritePropertyName(keys[i]);
                    WriteValue(writer, i < row.Count ? row[i] : null);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static string[] UniqueKeys(IReadOnlyList<string> columns)
    {
        var taken = new HashSet<string>(columns, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var keys = new string[columns.Count];
        for (var i = 0; i < keys.Length; i++)
        {
            var key = columns[i];
            for (var n = 2; !seen.Add(key); n++)
            {
                key = columns[i] + "_" + n.ToString(CultureInfo.InvariantCulture);
                if (taken.Contains(key))
                {
                    key = columns[i];
                }
            }

            keys[i] = key;
        }

        return keys;
    }

    private static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null or DBNull:
                writer.WriteNullValue();
                break;
            case bool flag:
                writer.WriteBooleanValue(flag);
                break;
            case string text:
                writer.WriteStringValue(text);
                break;
            case byte[] bytes:
                writer.WriteStringValue("\\x" + Convert.ToHexStringLower(bytes));
                break;
            case Array or System.Collections.IEnumerable when value is not string:
                WriteArray(writer, (System.Collections.IEnumerable)value);
                break;
            default:
                WriteScalar(writer, value);
                break;
        }
    }

    private static void WriteArray(Utf8JsonWriter writer, System.Collections.IEnumerable items)
    {
        writer.WriteStartArray();
        foreach (var item in items)
        {
            WriteValue(writer, item);
        }

        writer.WriteEndArray();
    }

    private static void WriteScalar(Utf8JsonWriter writer, object value)
    {
        switch (value)
        {
            case sbyte or byte or short or ushort or int or uint or long or ulong or decimal:
                writer.WriteRawValue(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null");
                break;
            case float or double:
                WriteFloating(writer, Convert.ToDouble(value, CultureInfo.InvariantCulture));
                break;
            case DateTime moment:
                writer.WriteStringValue(ValueText.Moment(moment));
                break;
            case DateTimeOffset moment:
                writer.WriteStringValue(ValueText.Moment(moment));
                break;
            case DateOnly day:
                writer.WriteStringValue(ValueText.Day(day));
                break;
            case TimeOnly time:
                writer.WriteStringValue(ValueText.Time(time));
                break;
            default:
                writer.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
        }
    }

    private static void WriteFloating(Utf8JsonWriter writer, double number)
    {
        if (double.IsFinite(number))
        {
            writer.WriteNumberValue(number);
        }
        else
        {
            writer.WriteStringValue(number.ToString(CultureInfo.InvariantCulture));
        }
    }
}
