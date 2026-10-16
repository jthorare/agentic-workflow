using System.Text.Encodings.Web;
using System.Text.Json;

namespace Aw.Evidence.Core.Data;

/// <summary>
/// A scrubber that also replaces, by value and wherever they appear, the values a result set read from secret-named columns (in the text and in its
/// JSON-escaped forms), then hands the text to the scrubber it wraps. A value too short to be told from ordinary text is left to the patterns.
/// </summary>
/// <param name="inner">The scrubber that runs after the replacement.</param>
/// <param name="values">The values read from secret-named columns.</param>
public sealed class FieldValueScrubber(ITextScrubber inner, IEnumerable<string> values) : ITextScrubber
{
    private readonly string[] _forms =
    [
        .. values.Where(static v => v.Length >= ValueScrubber.MinimumLength)
            .SelectMany(static v => new[] { v, JsonEncodedText.Encode(v).ToString(), JsonEncodedText.Encode(v, JavaScriptEncoder.UnsafeRelaxedJsonEscaping).ToString() })
            .Distinct(StringComparer.Ordinal).OrderByDescending(static v => v.Length),
    ];

    /// <inheritdoc/>
    public string Scrub(string text) =>
        inner.Scrub(_forms.Aggregate(text, static (current, form) => current.Replace(form, RecordedText.SecretField, StringComparison.Ordinal)));

    /// <inheritdoc/>
    public bool IsSecretName(string name) => inner.IsSecretName(name);
}
