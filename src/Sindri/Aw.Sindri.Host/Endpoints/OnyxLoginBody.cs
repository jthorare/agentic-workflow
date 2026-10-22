using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// The reader of POST /sindri/onyx/account's body (setup-once spec §5.6 "The password"): its own, straight into <see cref="OnyxLogin"/>, so the password never passes through the JSON binder, a model, a record's formatting or an error that
/// quotes its input. Whatever is wrong with the body, the answer is one of two fixed sentences; the body is never echoed.
/// </summary>
internal static class OnyxLoginBody
{
    /// <summary>The most bytes read; a login is a few dozen, so more is not a login.</summary>
    internal const int MaxBytes = 8 * 1024;

    private const string NotAnObject = "the request body isn't a JSON object with adminEmail and adminPassword (fix: send {\"adminEmail\": \"you@example.com\", \"adminPassword\": \"…\"})";

    /// <summary>Reads the login from <paramref name="body"/>.</summary>
    /// <param name="body">The request body.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The login; SND-CLI-003 with a fixed sentence for a body that is not a JSON object or is too long; SND-SETUP-005 when either field is missing, not a string or empty.</returns>
    internal static async ValueTask<Result<OnyxLogin>> Read(Stream body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var bytes = new byte[MaxBytes + 1];
        var length = 0;
        int read;
        while (length < bytes.Length && (read = await body.ReadAsync(bytes.AsMemory(length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            length += read;
        }

        try
        {
            if (length > MaxBytes)
            {
                return SindriErrors.InvalidInput.With(NotAnObject);
            }

            using var document = JsonDocument.Parse(bytes.AsMemory(0, length), new JsonDocumentOptions { MaxDepth = 4 });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return SindriErrors.InvalidInput.With(NotAnObject);
            }

            var email = Text(document.RootElement, "adminEmail")?.Trim();
            var password = Text(document.RootElement, "adminPassword");
            return email is { Length: > 0 } && password is { Length: > 0 }
                ? new OnyxLogin(email, new OnyxSecret(password))
                : SindriErrors.SetupOnyxCredentialsMissing.With(SindriErrors.SetupOnyxCredentialsMissing.Title);
        }
        catch (JsonException)
        {
            return SindriErrors.InvalidInput.With(NotAnObject);   // the parser's message can quote the input
        }
        finally
        {
            Array.Clear(bytes);   // the bytes held the password in the clear
        }
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
