using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Questions;

namespace Aw.Judge.Core.Evaluation;

/// <summary>
/// Caps the stored input and keeps it valid JSON. Input that fits is stored whole. Over the cap, the longest string
/// values are cut (each to the same length L, with a "[truncated N chars]" marker) and L is the largest length at which
/// the whole value fits, so a short field is never touched while a long one is; if even L = 0 doesn't fit (a huge array
/// of numbers, thousands of strings), the stored value is {"truncated": "&lt;prefix&gt;"}.
/// Redact first: a secret cut in half would no longer match its pattern.
/// </summary>
public static class InputCap
{
    /// <summary>The stored input's cap, in UTF-16 characters.</summary>
    public const int MaxChars = 16_000;

    /// <summary>Writes the value as JSON of at most <paramref name="maxChars"/> characters.</summary>
    /// <param name="value">The (redacted) value; not changed.</param>
    /// <param name="maxChars">The cap (at least 100).</param>
    /// <param name="options">The package's AwJson options.</param>
    /// <returns>Valid JSON text.</returns>
    public static string Write(JsonNode? value, int maxChars, JsonSerializerOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxChars, 100);
        var text = InputJson.Write(value, options);
        if (text.Length <= maxChars)
        {
            return text;
        }

        // The longest strings are cut first (FairCut); no length fits (even every string reduced to its marker is too big) -> the stub.
        var limit = FairCut.Limit(LongestString(value), l => InputJson.Write(CutStrings(value, l), options).Length <= maxChars);
        var cut = InputJson.Write(CutStrings(value, limit), options);
        return cut.Length <= maxChars ? cut : Stub(text, maxChars, options);
    }

    /// <summary>The text cut to <paramref name="maxChars"/> with a "[truncated N chars]" marker, or unchanged when it fits.</summary>
    /// <param name="text">The text.</param>
    /// <param name="maxChars">How many characters to keep (one fewer when that would split a surrogate pair).</param>
    /// <returns>The capped text.</returns>
    public static string Truncate(string text, int maxChars)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length <= maxChars)
        {
            return text;
        }

        var keep = maxChars > 0 && char.IsHighSurrogate(text[maxChars - 1]) ? maxChars - 1 : maxChars;
        return string.Create(CultureInfo.InvariantCulture, $"{text[..keep]}[truncated {text.Length - keep} chars]");
    }

    private static JsonNode? CutStrings(JsonNode? node, int limit) => node switch
    {
        JsonObject obj => new JsonObject(obj.Select(pair => KeyValuePair.Create(pair.Key, CutStrings(pair.Value, limit)))),
        JsonArray array => new JsonArray([.. array.Select(item => CutStrings(item, limit))]),
        JsonValue value when value.TryGetValue<string>(out var text) => JsonValue.Create(Truncate(text, limit)),
        null => null,
        _ => node.DeepClone(),
    };

    private static int LongestString(JsonNode? node) => node switch
    {
        JsonObject obj => obj.Select(pair => LongestString(pair.Value)).DefaultIfEmpty().Max(),
        JsonArray array => array.Select(LongestString).DefaultIfEmpty().Max(),
        JsonValue value when value.TryGetValue<string>(out var text) => text.Length,
        _ => 0,
    };

    // A prefix of the full text as one string; halved until its escaped form fits.
    private static string Stub(string text, int maxChars, JsonSerializerOptions options)
    {
        var keep = maxChars - 80;
        while (true)
        {
            var stub = InputJson.Write(new JsonObject { ["truncated"] = Truncate(text, keep) }, options);
            if (stub.Length <= maxChars)
            {
                return stub;
            }

            keep /= 2;
        }
    }
}
