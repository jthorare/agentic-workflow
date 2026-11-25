using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Ui.Api;

/// <summary>The app's error catalogue (SND-UI-NNN, docs/csharp/sindri-ui.md). The app shows each as words with a fix.</summary>
internal static class SindriUiErrors
{
    /// <summary>SND-UI-001: no usable endpoint record, or it names a process that isn't the one answering.</summary>
    public static readonly ErrorDefinition NotRunning = new("SND-UI-001", ErrorKind.Unavailable, "Sindri is not running");

    /// <summary>SND-UI-002: nothing answered at the recorded endpoint, or not within 120 s.</summary>
    public static readonly ErrorDefinition Unreachable = new("SND-UI-002", ErrorKind.Unavailable, "Sindri's API doesn't answer");

    /// <summary>SND-UI-002 for a call that passed its deadline: the words every such message carries, so a read can say it timed out instead of "unreachable".</summary>
    internal const string DeadlineWords = "didn't answer within";

    /// <summary>The message of an SND-UI-002 for a call that passed its deadline.</summary>
    /// <param name="authority">The API's host and port.</param>
    /// <param name="seconds">The deadline in seconds.</param>
    /// <returns>The message, carrying <see cref="DeadlineWords"/>.</returns>
    internal static string DeadlineMessage(string authority, double seconds) => string.Create(CultureInfo.InvariantCulture, $"{authority} {DeadlineWords} {seconds:0} s");

    /// <summary>Whether an error is the deadline passing, as against nothing answering at all.</summary>
    /// <param name="error">The error.</param>
    /// <returns>True for an SND-UI-002 whose message carries <see cref="DeadlineWords"/>.</returns>
    internal static bool IsDeadline(Error error) =>
        string.Equals(error.Code, Unreachable.Code, StringComparison.Ordinal) && error.Message.Contains(DeadlineWords, StringComparison.Ordinal);

    /// <summary>SND-UI-003: the API answered, but not with the record this build reads.</summary>
    public static readonly ErrorDefinition BadAnswer = new("SND-UI-003", ErrorKind.Invalid, "Sindri's API gave an answer this app can't read");

    /// <summary>SND-UI-003, for a status the API should not have answered with (a 500 with no problem body): the same code, but a server error is an unavailable one, not a body this build can't read.</summary>
    public static readonly ErrorDefinition BadStatus = new("SND-UI-003", ErrorKind.Unavailable, "Sindri's API gave an answer this app can't read");

    /// <summary>SND-UI-004: the app's start-up, or its following of the control plane, threw; the app exits with code 2.</summary>
    public static readonly ErrorDefinition Failed = new("SND-UI-004", ErrorKind.Unavailable, "The Sindri app failed and exits");
}
