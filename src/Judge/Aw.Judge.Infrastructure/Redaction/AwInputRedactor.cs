using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aw.Judge.Core.Ports;
using Aw.Redaction;

namespace Aw.Judge.Infrastructure.Redaction;

/// <summary>
/// IInputRedactor over Aw.Redaction: every string value and key goes through the shared scrubber, then through the
/// judge's own patterns (the TS redactSecrets set), which keep the standalone 40+ hex rule Aw.Redaction leaves out. A
/// field whose name says it holds a secret (<see cref="IRedactor.IsSecretName"/>: client_secret, apiKey, password) has
/// its whole value replaced, whatever it looks like: the key and the value are scrubbed apart, so the assignment rules
/// can't see the name.
/// </summary>
/// <param name="redactor">The shared scrubber.</param>
internal sealed partial class AwInputRedactor(IRedactor redactor) : IInputRedactor
{
    /// <summary>What a judge pattern's match becomes.</summary>
    public const string Placeholder = "[REDACTED]";

    private const int TimeoutMs = 1000;

    /// <inheritdoc />
    public JsonNode? Redact(JsonNode? input) => input switch
    {
        JsonObject obj => Object(obj),
        JsonArray array => new JsonArray([.. array.Select(Redact)]),
        JsonValue value when value.TryGetValue<string>(out var text) => JsonValue.Create(Text(text)),
        _ => input?.DeepClone(),
    };

    // An Anthropic or OpenAI key; a GitHub token; an AWS access key id; a Slack token; a JWT; a standalone 40+ hex run
    // (full git shas included; short shas in prose survive). The \b of the TS patterns is spelled as ASCII lookarounds.
    [GeneratedRegex("(?<![A-Za-z0-9_])sk-[A-Za-z0-9_-]{20,}", RegexOptions.None, TimeoutMs)]
    private static partial Regex OpenAiKey { get; }

    [GeneratedRegex("(?<![A-Za-z0-9_])gh[pousr]_[A-Za-z0-9]{30,}", RegexOptions.None, TimeoutMs)]
    private static partial Regex GitHubToken { get; }

    [GeneratedRegex("(?<![A-Za-z0-9_])AKIA[A-Z0-9]{16}(?![A-Za-z0-9_])", RegexOptions.None, TimeoutMs)]
    private static partial Regex AwsKeyId { get; }

    [GeneratedRegex("(?<![A-Za-z0-9_])xox[abprs]-[A-Za-z0-9-]{10,}", RegexOptions.None, TimeoutMs)]
    private static partial Regex SlackToken { get; }

    [GeneratedRegex("(?<![A-Za-z0-9_])eyJ[A-Za-z0-9_-]{8,}\\.[A-Za-z0-9_-]{8,}\\.[A-Za-z0-9_-]{8,}", RegexOptions.None, TimeoutMs)]
    private static partial Regex Jwt { get; }

    [GeneratedRegex("(?<![A-Za-z0-9_])[0-9a-fA-F]{40,}(?![A-Za-z0-9_])", RegexOptions.None, TimeoutMs)]
    private static partial Regex LongHex { get; }

    private static IEnumerable<Regex> JudgePatterns() => [OpenAiKey, GitHubToken, AwsKeyId, SlackToken, Jwt, LongHex];

    private JsonObject Object(JsonObject obj)
    {
        var copy = new JsonObject();
        foreach (var (key, value) in obj)
        {
            copy[Text(key)] = value is JsonValue && redactor.IsSecretName(key) ? JsonValue.Create(Placeholder) : Redact(value);
        }

        return copy;
    }

    // A text the scanner can't finish within its timeout is withheld whole, never stored as is.
    private string Text(string text)
    {
        var scrubbed = redactor.Scrub(text);
        if (scrubbed.IsBlocked)
        {
            return ScrubResult.BlockedText;
        }

        try
        {
            return JudgePatterns().Aggregate(scrubbed.Text, (acc, pattern) => pattern.Replace(acc, Placeholder));
        }
        catch (RegexMatchTimeoutException)
        {
            return ScrubResult.BlockedText;
        }
    }
}
