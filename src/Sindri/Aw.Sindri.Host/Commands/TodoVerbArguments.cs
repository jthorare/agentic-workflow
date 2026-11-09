using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Dates;
using Aw.Sindri.Core;

namespace Aw.Sindri.Host.Commands;

/// <summary>Reads the arguments of 'sindri todos' and 'sindri todo show|add|edit|done|reopen|remove|restore|keep|note' (todo spec §4.4) into one <see cref="Parsed"/>, or the usage error.</summary>
internal static class TodoVerbArguments
{
    internal const string Usage = "todos [--done | --all] [--json] [--fence | --no-fence] [--no-start] | todo show <id> [--json] | todo add <text> [--due DAY] [--link REF]... [--client-id ID] [--json] | todo edit <id> [--text T] [--due DAY | --no-due] [--link REF]... [--no-links] [--version N] | todo done|reopen|remove|restore <id> [--version N] | todo keep <id> sindri|obsidian [--json] | todo note [--json]";

    internal enum Verb
    {
        None,
        List,
        Show,
        Add,
        Edit,
        Done,
        Reopen,
        Remove,
        Restore,
        Keep,
        Note,
    }

    internal static Result<Parsed> Parse(string[] args, DateOnly today)
    {
        var (verb, rest) = args switch
        {
            ["todos", .. var r] => (Verb.List, r),
            ["todo", "show", .. var r] => (Verb.Show, r),
            ["todo", "add", .. var r] => (Verb.Add, r),
            ["todo", "edit", .. var r] => (Verb.Edit, r),
            ["todo", "done", .. var r] => (Verb.Done, r),
            ["todo", "reopen", .. var r] => (Verb.Reopen, r),
            ["todo", "remove", .. var r] => (Verb.Remove, r),
            ["todo", "restore", .. var r] => (Verb.Restore, r),
            ["todo", "keep", .. var r] => (Verb.Keep, r),
            ["todo", "note", .. var r] => (Verb.Note, r),
            _ => (Verb.None, args),
        };
        if (verb == Verb.None)
        {
            return SindriErrors.UnknownVerb.With($"'{string.Join(' ', args)}' is not a todo command: {Usage}");
        }

        var parsed = new Parsed(verb);
        var words = new List<string>();
        for (var i = 0; i < rest.Length; i++)
        {
            var taken = Flag(parsed, rest, i, words, today);
            if (taken.IsError)
            {
                return taken.Error;
            }

            i += taken.Value;
        }

        return Shape(parsed, words, today) is { } wrong ? wrong : parsed;
    }

    private static readonly HashSet<string> _valueFlags = new(StringComparer.Ordinal) { "--text", "--due", "--link", "--client-id", "--version" };

    private static Result<int> Flag(Parsed parsed, string[] rest, int i, List<string> words, DateOnly today)
    {
        var flag = rest[i];
        if (Switch(parsed, flag))
        {
            return 0;
        }

        if (_valueFlags.Contains(flag) && Takes(parsed.Verb, flag))
        {
            return i + 1 < rest.Length ? Valued(parsed, flag, rest[i + 1], today) : SindriErrors.InvalidInput.With($"{flag} needs a value: {Usage}");
        }

        if (flag.StartsWith("--", StringComparison.Ordinal))
        {
            return SindriErrors.InvalidInput.With($"'{flag}' isn't a flag of this command: {Usage}");
        }

        words.Add(flag);
        return 0;
    }

    // the flags that take no value; false when <paramref name="flag"/> is none of them for this verb
    private static bool Switch(Parsed parsed, string flag)
    {
        switch (flag)
        {
            case "--json":
                parsed.Json = true;
                return true;
            case "--no-start":
                parsed.NoStart = true;
                return true;
            case "--fence" or "--no-fence" when parsed.Verb == Verb.List:
                parsed.Fence = flag == "--fence";
                return true;
            case "--done" or "--all" when parsed.Verb == Verb.List:
                parsed.State = flag == "--done" ? "done" : "all";
                return true;
            case "--no-due" when parsed.Verb == Verb.Edit:
                parsed.ClearDue = true;
                return true;
            case "--no-links" when parsed.Verb == Verb.Edit:
                parsed.NoLinks = true;
                return true;
            default:
                return false;
        }
    }

    private static bool Takes(Verb verb, string flag) => flag switch
    {
        "--text" => verb == Verb.Edit,
        "--due" or "--link" => verb is Verb.Add or Verb.Edit,
        "--client-id" => verb == Verb.Add,
        _ => verb is Verb.Edit or Verb.Done or Verb.Reopen or Verb.Remove or Verb.Restore,   // --version
    };

    private static Result<int> Valued(Parsed parsed, string flag, string value, DateOnly today)
    {
        switch (flag)
        {
            case "--text":
                parsed.Text = value;
                break;
            case "--due":
                var due = DueWords.Resolve(value, today);
                if (due.IsError)
                {
                    return due.Error;
                }

                parsed.Due = due.Value;
                break;
            case "--link":
                parsed.Links.Add(value);
                break;
            case "--client-id":
                parsed.ClientId = value;
                break;
            default:
                if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var version))
                {
                    return SindriErrors.InvalidInput.With($"--version is a whole number, not '{value}': {Usage}");
                }

                parsed.Version = version;
                break;
        }

        return 1;
    }

    private static Error? Shape(Parsed parsed, List<string> words, DateOnly today) => parsed.Verb switch
    {
        Verb.List or Verb.Note => words.Count > 0 ? SindriErrors.InvalidInput.With($"'{string.Join(' ', words)}' isn't expected here: {Usage}") : null,
        Verb.Add => AddShape(parsed, words, today),
        Verb.Keep => KeepShape(parsed, words),
        Verb.Show or Verb.Edit or Verb.Done or Verb.Reopen or Verb.Remove or Verb.Restore => IdShape(parsed, words),
        Verb.None => null,
        _ => null,
    };

    private static Error? AddShape(Parsed parsed, List<string> words, DateOnly today)
    {
        if (words.Count == 0)
        {
            return SindriErrors.TodoText.With("Add the todo's text.");
        }

        var split = DueWords.Split(string.Join(' ', words), today);
        if (split.IsError)
        {
            return split.Error;
        }

        parsed.Text = split.Value.Text;
        parsed.Due ??= split.Value.Due;
        return null;
    }

    private static Error? KeepShape(Parsed parsed, List<string> words)
    {
        if (words.Count != 2 || words[1] is not ("sindri" or "obsidian"))
        {
            return SindriErrors.InvalidInput.With($"keep takes a todo id and the side to keep, sindri or obsidian: {Usage}");
        }

        parsed.Id = words[0];
        parsed.Side = words[1];
        return null;
    }

    private static Error? IdShape(Parsed parsed, List<string> words)
    {
        if (words.Count != 1)
        {
            return SindriErrors.InvalidInput.With($"{parsed.Verb.ToString().ToLowerInvariant()} takes one todo id: {Usage}");
        }

        parsed.Id = words[0];
        return parsed.Verb == Verb.Edit && parsed.Text is null && parsed.Due is null && !parsed.ClearDue && parsed.Links.Count == 0 && !parsed.NoLinks
            ? SindriErrors.TodoText.With("edit needs at least one of --text, --due, --no-due, --link, --no-links.")
            : null;
    }

    internal sealed class Parsed(Verb verb)
    {
        internal Verb Verb { get; } = verb;

        internal bool Json { get; set; }

        internal bool NoStart { get; set; }

        internal bool? Fence { get; set; }

        internal string State { get; set; } = "open";

        internal string Id { get; set; } = string.Empty;

        internal string? Text { get; set; }

        internal DateOnly? Due { get; set; }

        internal bool ClearDue { get; set; }

        internal List<string> Links { get; } = [];

        internal bool NoLinks { get; set; }

        internal string? ClientId { get; set; }

        internal string Side { get; set; } = string.Empty;

        internal long? Version { get; set; }
    }
}
