using System.Text.Json;
using Aw.Kernel;

namespace Aw.Sindri.Ui.Api.Context;

/// <summary>
/// The extra members of a context refusal (RFC 9457 §3.2): SND-CONTEXT-003 lists <c language="text">suggestedAnchors</c> and -037
/// lists <c language="text">candidates</c>; a library refusal (SND-LIB-001) names its <c language="text">field</c>. They ride on the error as extensions, as the API wrote them.
/// </summary>
internal static class ContextRefusal
{
    /// <summary>The error with the body's non-standard members attached, when it is a context refusal.</summary>
    /// <param name="error">The refusal read from the body.</param>
    /// <param name="body">The ProblemDetails body.</param>
    /// <returns>The error, extended, or as it was.</returns>
    internal static Error Extend(Error error, byte[] body)
    {
        ArgumentNullException.ThrowIfNull(error);
        // a job conflict carries the running job; a review settings or library refusal carries the request key it is about (field);
        // a Linear write refusal carries what the sheet needs to go on (the 409's fields and live issue, a 504's client id, a 429's retryAfter)
        if (!error.Code.StartsWith("SND-AGENT-", StringComparison.Ordinal) && !error.Code.StartsWith("SND-CONTEXT-", StringComparison.Ordinal) && !error.Code.StartsWith("SND-REVIEW-", StringComparison.Ordinal) && !error.Code.StartsWith("SND-LINEAR-", StringComparison.Ordinal) && !string.Equals(error.Code, "SND-JOB-409", StringComparison.Ordinal) && !string.Equals(error.Code, "SND-LIB-001", StringComparison.Ordinal))
        {
            return error;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            foreach (var member in document.RootElement.EnumerateObject().Where(m => ErrorExtensions.IsValidName(m.Name)))
            {
                error = error.WithExtension(member.Name, member.Value);
            }
        }
        catch (Exception e) when (e is JsonException or ArgumentException)
        {
            // why: a body that does not parse, or repeats a member name, has no extras to carry; the code and detail were already read from it
        }

        return error;
    }
}
