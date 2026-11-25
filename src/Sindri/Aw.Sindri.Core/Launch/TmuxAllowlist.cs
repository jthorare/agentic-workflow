using System.Text.RegularExpressions;
using Aw.Kernel;

namespace Aw.Sindri.Core.Launch;

/// <summary>
/// The commands Sindri sends to tmux (agents spec §15.2): a foreign list and a launch list. It limits only what Sindri sends; what the owner types in an attached pane is his.
/// A7 owns the class; A8 adds the terminal verbs' tests.
/// </summary>
public static partial class TmuxAllowlist
{
    private static readonly HashSet<string> _keys = new(StringComparer.Ordinal)
    {
        "Enter", "Escape", "Tab", "BTab", "BSpace", "DC", "Up", "Down", "Left", "Right", "Home", "End", "PageUp", "PageDown",
        "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
    };

    /// <summary>Checks one tmux call, as the arguments after 'tmux -L &lt;socket&gt;'.</summary>
    /// <param name="list">The list the socket's owner decides.</param>
    /// <param name="args">The tmux verb and its arguments.</param>
    /// <returns>Success, or an invalid error that names the refused verb, flag, key or target.</returns>
    public static Result<Unit> Check(TmuxList list, IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Count == 0)
        {
            return Refuse("an empty tmux call");
        }

        var launch = list == TmuxList.Launch;
        var rest = args.Skip(1).ToArray();
        var problem = args[0] switch
        {
            "capture-pane" => CapturePane(rest),
            "send-keys" => SendKeys(rest),
            "load-buffer" => rest is ["-b", "sindri-paste", "-"] ? null : "load-buffer takes only '-b sindri-paste -'",
            "paste-buffer" => PasteBuffer(rest),
            "display-message" => DisplayMessage(rest),
            "list-clients" => Flags(rest, valued: ["-t", "-F"], bare: [], mustHave: ["-t"], target: "-t"),
            "list-panes" => ListPanes(rest, launch),
            "new-session" when launch => NewSession(rest),
            "set-option" when launch => rest is ["-t", { } target, "remain-on-exit", "on"] ? OwnedSession(target) : "set-option takes only '-t <session> remain-on-exit on'",
            "resize-window" when launch => ResizeWindow(rest),
            "has-session" when launch => rest is ["-t", { } target] ? Target(target) : "has-session takes only '-t <target>'",
            "list-sessions" when launch => rest is [] or ["-F", _] ? null : "list-sessions takes only '-F <format>'",
            "kill-session" when launch => rest is ["-t", { } target] ? KillTarget(target) : "kill-session takes only '-t <session>'",
            _ => $"'{args[0]}' is not on the {(launch ? "launch" : "foreign")} list",
        };
        return problem is null ? Unit.Value : Refuse(problem);
    }

    private static Error Refuse(string reason) => KernelErrors.Invalid.With("tmux call refused: " + reason);

    private static string? CapturePane(string[] args)
    {
        var seen = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-e" or "-p":
                    break;
                case "-t" when i + 1 < args.Length:
                    if (Target(args[++i]) is { } bad)
                    {
                        return bad;
                    }

                    seen = true;
                    break;
                case "-S" or "-E" when i + 1 < args.Length && Number.IsMatch(args[i + 1]):
                    i++;
                    break;
                default:
                    return $"capture-pane does not take '{args[i]}'";
            }
        }

        return seen ? null : "capture-pane needs -t";
    }

    private static string? SendKeys(string[] args)
    {
        if (args is not ["-t", { } target, .. var tail])
        {
            return "send-keys needs '-t <target>' first";
        }

        if (Target(target) is { } bad)
        {
            return bad;
        }

        return tail switch
        {
            ["-l", "--", _] => null,
            [{ } key] when IsKey(key) => null,
            [{ } key] => $"'{key}' is not a key on the list",
            _ => "send-keys takes '-l -- <text>' or one key name",
        };
    }

    private static bool IsKey(string key) => _keys.Contains(key) || ModifiedKey.IsMatch(key);

    private static string? PasteBuffer(string[] args)
    {
        string? buffer = null;
        string? target = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-p" or "-d":
                    break;
                case "-b" when i + 1 < args.Length:
                    buffer = args[++i];
                    break;
                case "-t" when i + 1 < args.Length:
                    target = args[++i];
                    break;
                default:
                    return $"paste-buffer does not take '{args[i]}'";
            }
        }

        return buffer != "sindri-paste" ? "paste-buffer uses only the buffer sindri-paste" : target is null ? "paste-buffer needs -t" : Target(target);
    }

    private static string? DisplayMessage(string[] args) =>
        args is ["-p", "-t", { } target, { } format] && !format.StartsWith('-') ? Target(target) : "display-message takes only '-p -t <target> <format>'";

    private static string? ListPanes(string[] args, bool launch) =>
        launch && args is ["-a", "-F", _]
            ? null
            : Flags(args, valued: ["-t", "-F"], bare: ["-s"], mustHave: ["-t"], target: "-t");

    private static string? ResizeWindow(string[] args) =>
        args is ["-t", { } target, "-x", { } w, "-y", { } h] && Size(w) && Size(h) ? OwnedSession(target) : "resize-window takes only '-t <session> -x W -y H'";

    private static bool Size(string value) => Digits.IsMatch(value) && value.Length <= 4;

    private static string? NewSession(string[] args)
    {
        string? name = null;
        var detached = false;
        var i = 0;
        for (; i < args.Length; i++)
        {
            if (args[i] == "--")
            {
                i++;
                break;
            }

            if (!args[i].StartsWith('-'))
            {
                break;   // the command starts here ('sh <script>')
            }

            if (NewSessionFlag(args, ref i, ref name, ref detached) is { } problem)
            {
                return problem;
            }
        }

        return CommandPart(name, detached, args.Length - i);
    }

    private static string? NewSessionFlag(string[] args, ref int i, ref string? name, ref bool detached)
    {
        var hasValue = i + 1 < args.Length;
        switch (args[i])
        {
            case "-d":
                detached = true;
                return null;
            case "-P":
                return null;
            case "-s" when hasValue:
                name = args[++i];
                return null;
            case "-x" or "-y" when hasValue && Size(args[i + 1]):
                i++;
                return null;
            case "-c" or "-F" or "-e" when hasValue && args[i + 1].Length > 0:
                i++;
                return null;
            default:
                return $"new-session does not take '{args[i]}'";
        }
    }

    private static string? CommandPart(string? name, bool detached, int commandLength) =>
        !detached ? "new-session must be detached (-d)"
        : name is null || !OwnedName.IsMatch(name) ? "new-session names its session sindri-<id> or snd-<id>"
        : commandLength == 0 ? "new-session needs a command"
        : null;

    private static string? KillTarget(string target)
    {
        var name = target.StartsWith('=') ? target[1..] : target;
        return KillName.IsMatch(name) ? null : "kill-session ends only a session named sindri-<actId>";
    }

    private static string? OwnedSession(string target) => OwnedName.IsMatch(target.StartsWith('=') ? target[1..] : target) ? null : "that session is not one Sindri started";

    private static string? Target(string target) => PaneOrSession.IsMatch(target) ? null : $"'{target}' is neither a pane id (%N) nor a session name";

    /// <summary>Walks flags: each valued flag takes one value, each bare flag none; anything else is refused.</summary>
    private static string? Flags(string[] args, string[] valued, string[] bare, string[] mustHave, string target)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            if (bare.Contains(args[i], StringComparer.Ordinal))
            {
                _ = seen.Add(args[i]);
            }
            else if (valued.Contains(args[i], StringComparer.Ordinal) && i + 1 < args.Length)
            {
                _ = seen.Add(args[i]);
                if (args[i] == target && Target(args[i + 1]) is { } bad)
                {
                    return bad;
                }

                i++;
            }
            else
            {
                return $"'{args[i]}' is not allowed here";
            }
        }

        return mustHave.All(seen.Contains) ? null : $"needs {string.Join(", ", mustHave)}";
    }

    [GeneratedRegex(@"^(%[0-9]+|=?[A-Za-z0-9_.-]{1,64})\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PaneOrSession { get; }

    [GeneratedRegex(@"^(sindri-[a-z0-9]{8,32}|snd-[A-Za-z0-9_-]{1,64})\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex OwnedName { get; }

    [GeneratedRegex(@"^sindri-[a-z0-9]{8,32}\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex KillName { get; }

    [GeneratedRegex(@"^(C|M)-[a-z]\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ModifiedKey { get; }

    [GeneratedRegex(@"^-?[0-9]+\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Number { get; }

    [GeneratedRegex(@"^[0-9]+\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Digits { get; }
}
