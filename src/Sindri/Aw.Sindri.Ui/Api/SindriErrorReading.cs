using System.Globalization;
using System.Net;
using Aw.Kernel;

namespace Aw.Sindri.Ui.Api;

/// <summary>Reads the app's own errors where the client keeps no field for it.</summary>
internal static class SindriErrorReading
{
    /// <summary>True when the client read a bare 404 (a 404 with no Sindri problem body). The client's BadStatus carries no status, only "{path} answered {code}", so this and BadStatusMessage are the only places the text is read or built.</summary>
    /// <param name="error">The failed call's error.</param>
    /// <returns>True for a bare 404.</returns>
    internal static bool IsNotFound(this Error error) =>
        string.Equals(error.Code, SindriUiErrors.BadStatus.Code, StringComparison.Ordinal) && error.Message.EndsWith(AnsweredSuffix(HttpStatusCode.NotFound), StringComparison.Ordinal);

    /// <summary>True when the client read a bare 400 (a 400 with no Sindri problem body), such as a refused malformed id. Read the same way as <see cref="IsNotFound"/>, so the status text stays in this class.</summary>
    /// <param name="error">The failed call's error.</param>
    /// <returns>True for a bare 400.</returns>
    internal static bool IsBadRequest(this Error error) =>
        string.Equals(error.Code, SindriUiErrors.BadStatus.Code, StringComparison.Ordinal) && error.Message.EndsWith(AnsweredSuffix(HttpStatusCode.BadRequest), StringComparison.Ordinal);

    /// <summary>The message of the client's BadStatus error: the path and the status it answered. The only place this text is built.</summary>
    /// <param name="path">The route asked.</param>
    /// <param name="code">The status answered.</param>
    /// <returns>The message.</returns>
    internal static string BadStatusMessage(string path, HttpStatusCode code) => path + AnsweredSuffix(code);

    private static string AnsweredSuffix(HttpStatusCode code) => string.Create(CultureInfo.InvariantCulture, $" answered {(int)code}");
}
