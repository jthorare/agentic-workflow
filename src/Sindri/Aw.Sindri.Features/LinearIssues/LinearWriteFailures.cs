using System.Buffers;
using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Linear;
using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.LinearIssues;

/// <summary>How a failure of Context's Linear writes reads on Sindri's routes (Linear hub spec §8): a session's refusal is SND-AUTH-002, a missing write key is 403, and a write Linear never confirmed is 504 with the client id to retry with. Every other code comes through as Context gave it.</summary>
internal static class LinearWriteFailures
{
    internal const string Refusal = "Linear writes are the owner's to make; a session can't (fix: use the app)";

    /// <summary>Maps one of Context's errors.</summary>
    /// <param name="error">The error Context gave.</param>
    /// <returns>The error the route answers with.</returns>
    internal static Error Of(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error.Code switch
        {
            var code when string.Equals(code, ContextErrors.SessionsDisabled.Code, StringComparison.Ordinal) => SindriErrors.AuthRefused.With(Refusal),
            var code when string.Equals(code, LinearHubErrors.NoWriteKey.Code, StringComparison.Ordinal) => SindriErrors.LinearNoWriteKey.With("Sindri has no Linear write key (fix: Settings, Connections, Linear, add a write key)"),
            _ => error,
        };
    }

    /// <summary>A write that was sent and whose outcome nobody could read back.</summary>
    /// <param name="message">What was not confirmed, with the fix.</param>
    /// <param name="clientId">The client id the retry sends again.</param>
    /// <returns>SND-LINEAR-011 with the members <c language="text">outcome</c> ('unknown') and <c language="text">clientId</c>.</returns>
    internal static Error NoAnswer(string message, string clientId) =>
        SindriErrors.LinearNoAnswer.With(message).WithExtension("outcome", Text("unknown")).WithExtension("clientId", Text(clientId));

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
