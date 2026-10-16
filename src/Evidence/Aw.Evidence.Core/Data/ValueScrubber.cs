using System.Text.Encodings.Web;
using System.Text.Json;

namespace Aw.Evidence.Core.Data;

/// <summary>
/// A run's scrubber: the secret values the run itself resolved (header values, pass-through variables and the like) are replaced
/// wherever they appear, in the text and in its JSON-escaped forms, before the pattern scrub runs; each kind has its own placeholder.
/// A value too short to be told from ordinary text is left to the patterns.
/// </summary>
/// <param name="patterns">The pattern scrubber.</param>
/// <param name="headerValues">Header values the run resolved (a credential with a scheme is hidden whole and by its credential alone).</param>
/// <param name="envValues">Values of passed-through variables and other resolved secrets.</param>
public sealed class ValueScrubber(ITextScrubber patterns, IEnumerable<string> headerValues, IEnumerable<string> envValues) : ITextScrubber
{
    /// <summary>What replaces a header value.</summary>
    public const string Placeholder = "[REDACTED:header-value]";

    /// <summary>What replaces a passed-through variable's value.</summary>
    public const string EnvPlaceholder = "[REDACTED:env-value]";

    /// <summary>Values shorter than this are not scrubbed by value: they would hit ordinary words.</summary>
    public const int MinimumLength = 6;

    private readonly (string Value, string Placeholder)[] _values =
    [
        .. headerValues.SelectMany(CredentialForms).Select(static v => (Value: v, Placeholder)).Concat(envValues.Select(static v => (Value: v, Placeholder: EnvPlaceholder)))
            .Where(static e => e.Value.Length >= MinimumLength).SelectMany(static e => Forms(e.Value).Select(form => (Value: form, e.Placeholder)))
            .Distinct().OrderByDescending(static e => e.Value.Length),
    ];

    /// <inheritdoc/>
    public string Scrub(string text) =>
        patterns.Scrub(_values.Aggregate(text, static (current, entry) => current.Replace(entry.Value, entry.Placeholder, StringComparison.Ordinal)));

    /// <inheritdoc/>
    public bool IsSecretName(string name) => patterns.IsSecretName(name);

    // A '<scheme> <credential>' value (Bearer abc) is hidden whole and by its credential alone, which an app may echo without the scheme.
    private static IEnumerable<string> CredentialForms(string value)
    {
        yield return value;
        var space = value.IndexOf(' ', StringComparison.Ordinal);
        if (space > 0 && space < value.Length - 1)
        {
            yield return value[(space + 1)..].Trim();
        }
    }

    private static IEnumerable<string> Forms(string value)
    {
        yield return value;
        yield return JsonEncodedText.Encode(value).ToString();
        yield return JsonEncodedText.Encode(value, JavaScriptEncoder.UnsafeRelaxedJsonEscaping).ToString();
    }
}
