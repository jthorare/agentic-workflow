using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Workflow;

/// <summary>
/// RFC 8785 (JSON Canonicalization Scheme): what the recipe hash (spec §3.1) and the descriptor hash (spec §2) are
/// computed over. Object members sorted by their names' UTF-16 code units, no whitespace, strings with only the
/// escapes JSON requires ('"', '\', and the control characters: \b \t \n \f \r by name, the rest as lower-case \u00xx),
/// everything else as UTF-8, and numbers as an IEEE 754 double written the way ECMAScript's Number.prototype.toString
/// writes it ('4.50' is '4.5', '1E30' is '1e+30', '2e-3' is '0.002').
/// </summary>
public static class CanonicalJson
{
    /// <summary>The canonical UTF-8 bytes of <paramref name="node"/>.</summary>
    /// <param name="node">A JSON value; null is JSON null.</param>
    /// <returns>The canonical form.</returns>
    /// <exception cref="ArgumentException">A number is NaN or infinite, which JSON can't hold.</exception>
    public static byte[] Of(JsonNode? node)
    {
        var text = new StringBuilder();
        Write(text, node);
        return Encoding.UTF8.GetBytes(text.ToString());
    }

    private static void Write(StringBuilder text, JsonNode? node)
    {
        switch (node)
        {
            case null:
                _ = text.Append("null");
                break;
            case JsonObject obj:
                WriteObject(text, obj);
                break;
            case JsonArray array:
                WriteArray(text, array);
                break;
            default:
                WriteValue(text, node.AsValue());
                break;
        }
    }

    private static void WriteObject(StringBuilder text, JsonObject obj)
    {
        _ = text.Append('{');
        var first = true;
        foreach (var (key, value) in obj.OrderBy(static p => p.Key, StringComparer.Ordinal))
        {
            _ = first ? text : text.Append(',');
            first = false;
            WriteString(text, key);
            _ = text.Append(':');
            Write(text, value);
        }

        _ = text.Append('}');
    }

    private static void WriteArray(StringBuilder text, JsonArray array)
    {
        _ = text.Append('[');
        for (var i = 0; i < array.Count; i++)
        {
            _ = i == 0 ? text : text.Append(',');
            Write(text, array[i]);
        }

        _ = text.Append(']');
    }

    private static void WriteValue(StringBuilder text, JsonValue value)
    {
        switch (value.GetValueKind())
        {
            case JsonValueKind.String:
                WriteString(text, value.GetValue<string>());
                break;
            case JsonValueKind.Number:
                _ = text.Append(Number(value.GetValue<double>()));
                break;
            case JsonValueKind.True:
                _ = text.Append("true");
                break;
            case JsonValueKind.False:
                _ = text.Append("false");
                break;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
            case JsonValueKind.Object:
            case JsonValueKind.Array:
            default:
                _ = text.Append("null");
                break;
        }
    }

    private static void WriteString(StringBuilder text, string value)
    {
        _ = text.Append('"');
        foreach (var c in value)
        {
            _ = Escape(c) is { } escape ? text.Append(escape) : text.Append(c);
        }

        _ = text.Append('"');
    }

    private static string? Escape(char c) => c switch
    {
        '"' => "\\\"",
        '\\' => "\\\\",
        '\b' => "\\b",
        '\t' => "\\t",
        '\n' => "\\n",
        '\f' => "\\f",
        '\r' => "\\r",
        < ' ' => "\\u" + ((int)c).ToString("x4", CultureInfo.InvariantCulture),
        _ => null,
    };

    /// <summary>ECMAScript's Number.prototype.toString for a finite double (RFC 8785 §3.2.2.3).</summary>
    private static string Number(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentException("JSON can't hold NaN or an infinite number.", nameof(value));
        }

        if (value == 0)
        {
            return "0";   // -0 too
        }

        if (value < 0)
        {
            return "-" + Number(-value);
        }

        var (digits, n) = Decompose(value);
        return Format(digits, n);
    }

    /// <summary>The shortest round-trip digits of a positive double, and n such that value = 0.d1d2…dk × 10^n.</summary>
    private static (string Digits, int N) Decompose(double value)
    {
        var shortest = value.ToString("R", CultureInfo.InvariantCulture);
        var e = shortest.IndexOf('E', StringComparison.Ordinal);
        var mantissa = e < 0 ? shortest : shortest[..e];
        var exponent = e < 0 ? 0 : int.Parse(shortest.AsSpan(e + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        var dot = mantissa.IndexOf('.', StringComparison.Ordinal);
        var whole = dot < 0 ? mantissa : mantissa[..dot];
        var all = dot < 0 ? mantissa : whole + mantissa[(dot + 1)..];
        var leading = all.Length - all.TrimStart('0').Length;
        return (all.Trim('0'), whole.Length + exponent - leading);
    }

    private static string Format(string digits, int n)
    {
        var k = digits.Length;
        if (k <= n && n <= 21)
        {
            return digits + new string('0', n - k);
        }

        if (n is > 0 and <= 21)
        {
            return digits[..n] + "." + digits[n..];
        }

        if (n is > -6 and <= 0)
        {
            return "0." + new string('0', -n) + digits;
        }

        var power = n - 1;
        var exponent = (power < 0 ? "-" : "+") + Math.Abs(power).ToString(CultureInfo.InvariantCulture);
        return k == 1 ? $"{digits}e{exponent}" : $"{digits[0]}.{digits[1..]}e{exponent}";
    }
}
