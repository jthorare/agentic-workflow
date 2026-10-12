using System.Text.Json.Nodes;
using Aw.Judge.Core.Ports;

namespace Aw.Judge.Testing;

/// <summary>
/// The IInputRedactor fake: replaces known secrets (by default <see cref="SampleSecrets.Contract"/>) in every string and
/// key, and the whole value of a field whose name has 'secret', 'password', 'token' or 'key' in it (any case); it passes
/// InputRedactorContract.
/// </summary>
/// <param name="secrets">The secrets it knows; the contract's samples when none are given.</param>
public sealed class FakeInputRedactor(params string[] secrets) : IInputRedactor
{
    /// <summary>What a secret becomes.</summary>
    public const string Placeholder = "[REDACTED]";

    private readonly IReadOnlyList<string> _secrets = secrets.Length == 0 ? SampleSecrets.Contract : secrets;

    /// <inheritdoc />
    public JsonNode? Redact(JsonNode? input) => input switch
    {
        JsonObject obj => Object(obj),
        JsonArray array => new JsonArray([.. array.Select(Redact)]),
        JsonValue value when value.TryGetValue<string>(out var text) => JsonValue.Create(Text(text)),
        _ => input?.DeepClone(),
    };

    private JsonObject Object(JsonObject obj)
    {
        var copy = new JsonObject();
        foreach (var (key, value) in obj)
        {
            copy[Text(key)] = value is JsonValue && IsSecretName(key) ? JsonValue.Create(Placeholder) : Redact(value);
        }

        return copy;
    }

    private static bool IsSecretName(string name) =>
        _secretWords.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase));

    private static readonly string[] _secretWords = ["secret", "password", "token", "key"];

    private string Text(string text) => _secrets.Aggregate(text, (acc, secret) => acc.Replace(secret, Placeholder, StringComparison.Ordinal));
}
