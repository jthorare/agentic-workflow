using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): what a send, an approval and a refusal say. Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>What a view says when Sindri refuses or can't answer its read.</summary>
    /// <param name="error">The failure as served (a refusal's code and detail, or the client's own).</param>
    /// <returns>The detail and the code.</returns>
    internal static string InstanceRefused(Error error) => $"{Capitalised(error.Message)} · {error.Code}";

    /// <summary>A sentence with its first letter upper-case, so a reason reads right after a lead like "Couldn't create the workflow." Only a plain first word (letters only, up to the first space) changes.</summary>
    /// <param name="text">The sentence.</param>
    /// <returns>"no run 'x'" becomes "No run 'x'"; unchanged when the first word holds anything but letters: "review.login is ...", "workflow:triage ...", "gh_token ...", "gh-2550 ...", "'docs' ...", "v2 ...", "2 runs ...".</returns>
    internal static string Capitalised(string text)
    {
        var space = text.IndexOf(' ', StringComparison.Ordinal);
        var word = space < 0 ? text : text[..space];
        return word.Length > 0 && word.All(char.IsLetter) && char.IsLower(word[0]) ? char.ToUpperInvariant(text[0]) + text[1..] : text;
    }

    /// <summary>The link to a subject outside the app.</summary>
    /// <param name="host">The link's host.</param>
    /// <returns>The words.</returns>
    internal static string OpenIn(string host) => $"Open in {host} ↗";

    /// <summary>A group whose rows show, in words.</summary>
    internal const string GroupExpanded = "expanded";

    /// <summary>A group collapsed to its count, in words.</summary>
    internal const string GroupCollapsed = "collapsed";

    /// <summary>What Open says when the file is not on this machine (#3714).</summary>
    internal const string FileNotOnMachine = "This file isn't on this machine.";

    /// <summary>What Open says when the file is there but the system has no app to open it (#3714).</summary>
    /// <param name="extension">The file's extension with its dot, or empty for none.</param>
    /// <returns>The words.</returns>
    internal static string NoAppOpens(string extension) => $"No app on this machine opens {(extension.Length > 0 ? extension + " files" : "this file")}.";

    /// <summary>A refused approval as the app shows it: the code, the words, and the row's served command to run instead; never a silent failure.</summary>
    /// <param name="code">The error code; empty for none.</param>
    /// <param name="words">What went wrong, in words.</param>
    /// <param name="cli">The row's served command; empty when it served none.</param>
    /// <returns>The line.</returns>
    internal static string ApproveRefusal(string code, string words, string cli) =>
        (code.Length > 0 ? code + ": " : "") + words + (cli.Length > 0 ? " Run: " + cli : "");

    /// <summary>A step Sindri launched itself answered: Sindri's own steps are refused.</summary>
    internal const string ApproveSessionRefused = "Sindri's own steps can't answer this.";

    /// <summary>The profile approval's button.</summary>
    internal const string ApproveSettings = "Approve settings";

    /// <summary>Said on a row while its command is out.</summary>
    internal const string RowSending = "Sending...";

    /// <summary>Said on a row when its approval went through.</summary>
    internal const string RowApprovalSent = "Approved.";

    /// <summary>Said on a row whose action the app cannot send yet; the command stands in for it.</summary>
    /// <param name="cli">The row's command, or empty.</param>
    /// <returns>The sentence.</returns>
    internal static string RowNotAvailable(string cli) => cli.Length > 0 ? $"Not available here yet. Run: {cli}" : "Not available here yet.";

    /// <summary>Who started a run or filed a request, in words.</summary>
    /// <param name="startedBy">The served value ("human:cli", "session:...", "service:host").</param>
    /// <returns>"you", "an agent session", "Sindri", or the served value for any other.</returns>
    internal static string StartedByWords(string startedBy) => startedBy switch
    {
        _ when startedBy.StartsWith("human", StringComparison.Ordinal) => "you",
        "service:host" => "Sindri",
        _ when startedBy.StartsWith("service:", StringComparison.Ordinal) => "Sindri's " + startedBy["service:".Length..] + " service",
        _ when startedBy.StartsWith("session:", StringComparison.Ordinal) => "an agent session",
        _ => startedBy,
    };

    /// <summary>A length of time as the engine says it: days, hours, minutes, leading zeros left out.</summary>
    /// <param name="seconds">The seconds.</param>
    /// <returns>"1d 23h 34m", "2h 36m", "40s".</returns>
    internal static string Duration(long seconds)
    {
        var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
        if (span.TotalMinutes < 1)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{(int)span.TotalSeconds}s");
        }

        var parts = new List<string>();
        if (span.Days > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{span.Days}d"));
        }

        if (span.Days > 0 || span.Hours > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{span.Hours}h"));
        }

        parts.Add(string.Create(CultureInfo.InvariantCulture, $"{span.Minutes}m"));
        return string.Join(' ', parts);
    }
}
