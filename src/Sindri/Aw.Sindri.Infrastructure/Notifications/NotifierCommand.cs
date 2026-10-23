using System.Text.RegularExpressions;

namespace Aw.Sindri.Infrastructure.Notifications;

/// <summary>
/// The process that shows a banner (see <see cref="OsascriptNotifier"/>): 'terminal-notifier' when it is installed and the notice has a deep link
/// (a click runs 'sindri-ui &lt;link&gt;', which hands the link to the running app), else 'osascript', whose banner has no click action.
/// A link is used only when it is made of the characters a deep link has, so it can sit inside the shell command a click runs.
/// </summary>
internal static partial class NotifierCommand
{
    /// <summary>What names the app's binary, as for 'sindri' itself (docs/csharp/sindri-ui.md).</summary>
    internal const string UiVariable = "AW_SINDRI_UI";

    /// <summary>The program to start and its arguments.</summary>
    /// <param name="File">The program.</param>
    /// <param name="Arguments">Its arguments.</param>
    internal sealed record BannerProcess(string File, IReadOnlyList<string> Arguments);

    /// <summary>The command for a banner.</summary>
    /// <param name="title">The title.</param>
    /// <param name="text">The body.</param>
    /// <param name="deepLink">The deep link text a click should open; null or unsafe for none.</param>
    /// <param name="terminalNotifier">The full path of terminal-notifier when it is installed; null otherwise.</param>
    /// <param name="uiBinary">The full path of the app's binary.</param>
    /// <returns>The command.</returns>
    internal static BannerProcess For(string title, string text, string? deepLink, string? terminalNotifier, string uiBinary)
    {
        if (terminalNotifier is { Length: > 0 } && deepLink is { Length: > 0 } && Safe.IsMatch(deepLink))
        {
            return new BannerProcess(terminalNotifier, ["-title", title, "-message", text, "-execute", Quote(uiBinary) + " " + deepLink]);
        }

        return new BannerProcess("osascript", ["-e", "on run argv", "-e", "display notification (item 2 of argv) with title (item 1 of argv)", "-e", "end run", title, text]);
    }

    /// <summary>Where the app's binary is: the one AW_SINDRI_UI names, else 'sindri-ui' next to this program.</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <param name="baseDirectory">The directory this program runs from.</param>
    /// <returns>The path.</returns>
    internal static string UiBinary(Func<string, string?> environment, string baseDirectory) =>
        environment(UiVariable) is { Length: > 0 } path ? path : Path.Combine(baseDirectory, "sindri-ui");

    /// <summary>The full path of an executable found on PATH.</summary>
    /// <param name="name">The program's name.</param>
    /// <returns>The path; null when it is not on PATH.</returns>
    internal static string? OnPath(string name) =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory, name)).FirstOrDefault(File.Exists);

    private static string Quote(string path) => "'" + path.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    [GeneratedRegex("^[A-Za-z0-9/_.:@?=-]{1,200}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Safe { get; }
}
