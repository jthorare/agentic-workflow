using System.Buffers;
using System.Text.Json;
using Aw.Kernel;

namespace Aw.Sindri.Core.Sandbox;

/// <summary>Makes the error of a sandbox launch refusal: the catalogue code, the owner-facing detail, and the 'reason' (and 'source') extensions the engine and the app read.</summary>
public static class SandboxRefusal
{
    /// <summary>Makes the refusal error.</summary>
    /// <param name="definition">One of the SND-LAUNCH-014..021 entries.</param>
    /// <param name="reason">A reason from <see cref="SandboxReasons"/> (for -018 'http-' and a status).</param>
    /// <param name="detail">What happened this time (the failing argv, the missing tool), already scrubbed; or null.</param>
    /// <param name="source">The credential source for -017, or null.</param>
    /// <param name="details">Named values the owner copy fills in ('which', 'tool', 'path', 'cap', 'running', 'limit', 'time', 'name', 'status'): each becomes an extension of that name.</param>
    /// <returns>The error.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The pair isn't a defined (code, reason): a programming error.</exception>
    public static Error For(ErrorDefinition definition, string reason, string? detail = null, ModelLoginSource? source = null, IReadOnlyDictionary<string, string>? details = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(reason);
        if (!SandboxReasons.IsDefined(definition.Code, reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, $"{definition.Code} has no reason '{reason}'.");
        }

        var error = definition.With(detail is null ? $"{definition.Title} ({reason})" : $"{definition.Title} ({reason}): {detail}")
            .WithExtension("reason", Text(reason));
        error = source is { } s ? error.WithExtension("source", Text(SandboxWire.Name(s))) : error;
        return (details ?? new Dictionary<string, string>(StringComparer.Ordinal)).Aggregate(error, (e, d) => e.WithExtension(d.Key, Text(d.Value)));
    }

    /// <summary>Reads back the reason a sandbox refusal carries (Extensions aren't serialized with the error).</summary>
    /// <param name="error">Any error.</param>
    /// <returns>The reason, or null when the error isn't a sandbox refusal.</returns>
    public static string? ReasonOf(Error error) => Extension(error, "reason");

    /// <summary>Reads back the credential source a refusal carries (-017).</summary>
    /// <param name="error">Any error.</param>
    /// <returns>The source, or null when the error has none.</returns>
    public static ModelLoginSource? SourceOf(Error error) => Extension(error, "source") switch
    {
        "owner-copy" => ModelLoginSource.OwnerCopy,
        "separate-login" => ModelLoginSource.SeparateLogin,
        _ => null,
    };

    /// <summary>Reads back the credential source a refusal carries, as the text the ledger records ('owner-copy' or 'separate-login').</summary>
    /// <param name="error">Any error.</param>
    /// <returns>The source name, or null when the error has none.</returns>
    public static string? SourceTextOf(Error error) => SourceOf(error) is { } source ? SandboxWire.Name(source) : null;

    private static string? Extension(Error error, string name)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error.Extensions?.Where(e => string.Equals(e.Key, name, StringComparison.Ordinal) && e.Value.ValueKind == JsonValueKind.String).Select(e => e.Value.GetString()).FirstOrDefault();
    }

    private static JsonElement Text(string value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStringValue(value);
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }
}
