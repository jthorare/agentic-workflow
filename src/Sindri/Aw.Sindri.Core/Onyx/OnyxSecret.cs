using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Onyx;

/// <summary>
/// A secret the owner typed for Onyx (their password; setup-once spec §5.6 "The password"). It has no ToString of its own and no way into JSON: the converter refuses both directions, so no AwJson context, ledger payload, job result or response can carry it.
/// Redaction never sees it either, because it is never text until <see cref="Reveal"/> hands it to the one request body that needs it.
/// </summary>
[JsonConverter(typeof(OnyxSecretConverter))]
public sealed class OnyxSecret
{
    private readonly string _value;

    /// <summary>Initializes a new instance of the <see cref="OnyxSecret"/> class.</summary>
    /// <param name="value">The secret.</param>
    public OnyxSecret(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary>Gets a value indicating whether the secret is empty.</summary>
    public bool IsEmpty => _value.Length == 0;

    /// <summary>Says nothing about the secret, so a log or an exception message that formats it prints no part of it.</summary>
    /// <returns>A fixed word.</returns>
    public override string ToString() => "(secret)";

    /// <summary>The secret's text, for the request body that carries it and nothing else.</summary>
    /// <returns>The secret.</returns>
    public string Reveal() => _value;

    private sealed class OnyxSecretConverter : JsonConverter<OnyxSecret>
    {
        public override OnyxSecret Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException("a secret is never read from JSON");

        public override void Write(Utf8JsonWriter writer, OnyxSecret value, JsonSerializerOptions options) => throw new NotSupportedException("a secret is never written as JSON");
    }
}
