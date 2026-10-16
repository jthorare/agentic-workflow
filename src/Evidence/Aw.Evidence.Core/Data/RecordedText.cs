using System.Text;
using System.Text.Json;

namespace Aw.Evidence.Core.Data;

/// <summary>Text on its way into a summary: secret-named JSON members hidden, the rest scrubbed, then capped, so a cap never cuts a secret in half and leaves the rest unrecognisable.</summary>
public static class RecordedText
{
    /// <summary>The most characters of a body, stdout or rows a step records.</summary>
    public const int OutputLimit = 2000;

    /// <summary>The most characters of an expected or actual value a failure records.</summary>
    public const int ValueLimit = 200;

    /// <summary>The most characters a headline holds.</summary>
    public const int HeadlineLimit = 200;

    /// <summary>What replaces the value of a secret-named member (Aw.Redaction's own text for it).</summary>
    public const string SecretField = "[REDACTED:secret-field]";

    private const int ReaderDepth = 1000;

    /// <summary>Text on its way into a summary: secret-named JSON members hidden, then scrubbed, then capped.</summary>
    /// <param name="scrubber">The run's scrubber.</param>
    /// <param name="text">The text.</param>
    /// <param name="limit">The most characters kept.</param>
    /// <returns>The recorded text.</returns>
    public static string Of(ITextScrubber scrubber, string text, int limit)
    {
        var scrubbed = Scrubbed(scrubber, text);
        return scrubbed.Length <= limit ? scrubbed : string.Concat(scrubbed.AsSpan(0, limit - 1), "…");
    }

    /// <summary>A failed assertion with its quoted values hidden when the pointer runs through a secret-named member, and scrubbed and capped otherwise.</summary>
    /// <param name="scrubber">The run's scrubber.</param>
    /// <param name="failure">The failure.</param>
    /// <returns>The failure as recorded.</returns>
    public static ApiFailure Of(ITextScrubber scrubber, ApiFailure failure)
    {
        var secret = failure.Location is not null && failure.Location.Split('/').Any(segment => scrubber.IsSecretName(segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal)));
        return failure with
        {
            Location = failure.Location is null ? null : Of(scrubber, failure.Location, ValueLimit),
            Expected = secret ? SecretField : Of(scrubber, failure.Expected, ValueLimit),
            Actual = secret ? SecretField : Of(scrubber, failure.Actual, ValueLimit),
        };
    }

    // Members are hidden first, on the text as written: a value scrub may put an unquoted placeholder where a learned numeric secret was, which makes the JSON invalid and would switch member hiding off.
    // The streaming pass keeps every other byte as written, so the value scrub afterwards still finds what it knows.
    private static string Scrubbed(ITextScrubber scrubber, string text) => scrubber.Scrub(HideSecretMembers(scrubber, text));

    // A streaming pass over the bytes, not a parsed tree: a tree cannot hold a repeated key (a body or stdout may repeat one).
    // The value of every secret-named property, wherever it occurs and however deep (a nested object or array too), is replaced in place by the placeholder; the rest of the text is kept as written.
    // Text that is not JSON keeps only the value and pattern scrub.
    private static string HideSecretMembers(ITextScrubber scrubber, string text)
    {
        var trimmed = text.AsSpan().Trim();
        if (trimmed.Length == 0 || (trimmed[0] != '{' && trimmed[0] != '['))
        {
            return text;
        }

        var bytes = Encoding.UTF8.GetBytes(text);
        var spans = new List<(int Start, int End)>();
        try
        {
            var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { MaxDepth = ReaderDepth });
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.PropertyName && scrubber.IsSecretName(reader.GetString() ?? string.Empty) && reader.Read())
                {
                    var start = (int)reader.TokenStartIndex;
                    reader.Skip();
                    spans.Add((start, (int)reader.BytesConsumed));
                }
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
        {
            return text;
        }

        return spans.Count == 0 ? text : Replaced(bytes, spans);
    }

    private static string Replaced(byte[] bytes, List<(int Start, int End)> spans)
    {
        var placeholder = Encoding.UTF8.GetBytes("\"" + SecretField + "\"");
        var result = new List<byte>(bytes.Length);
        var at = 0;
        foreach (var (start, end) in spans)
        {
            result.AddRange(bytes.AsSpan(at, start - at));
            result.AddRange(placeholder);
            at = end;
        }

        result.AddRange(bytes.AsSpan(at));
        return Encoding.UTF8.GetString([.. result]);
    }
}
