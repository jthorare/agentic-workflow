using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Projects;

namespace Aw.Sindri.Host.Commands;

/// <summary>Reads the arguments of the project write verbs ('project add', 'edit', 'remove', 'hide', 'unhide'; projects page spec §5.3) into one <see cref="Parsed"/>, or the usage error.</summary>
internal static class ProjectVerbArguments
{
    internal const string Usage = "project add [--linear KEY] [--name T] [--link URL]... [--state S] [--note T] [--target YYYY-MM-DD] [--json] [--no-start] | project edit <project> --version N [--name T] [--link URL]... [--state S | --clear-state] [--note T | --clear-note] [--target D | --clear-target] [--json] [--no-start] | project remove <project> --version N [--json] [--no-start] | project hide|unhide <project> --version N [--json] [--no-start]";

    internal const string NeedsVersion = "pass --version N (the version `sindri projects` will show; reading it for you comes with `sindri projects`)";

    internal static Result<Parsed> Parse(string[] args)
    {
        var (verb, rest) = args switch
        {
            ["project", "add", .. var r] => (Verb.Add, r),
            ["project", "edit", .. var r] => (Verb.Edit, r),
            ["project", "remove", .. var r] => (Verb.Remove, r),
            ["project", "hide", .. var r] => (Verb.Hide, r),
            ["project", "unhide", .. var r] => (Verb.Unhide, r),
            _ => (Verb.None, args),
        };
        if (verb == Verb.None)
        {
            return SindriErrors.UnknownVerb.With($"'{string.Join(' ', args)}' is not a project command: {Usage}");
        }

        var parsed = new Parsed(verb);
        var words = new List<string>();
        for (var i = 0; i < rest.Length; i++)
        {
            var taken = Flag(parsed, rest, i, words);
            if (taken.IsError)
            {
                return taken.Error;
            }

            i += taken.Value;
        }

        return Shape(parsed, words) is { } wrong ? wrong : parsed;
    }

    private static Result<int> Flag(Parsed parsed, string[] rest, int i, List<string> words)
    {
        var flag = rest[i];
        switch (flag)
        {
            case "--json":
                parsed.Json = true;
                return 0;
            case "--no-start":
                parsed.NoStart = true;
                return 0;
            case "--clear-state" or "--clear-note" or "--clear-target" when parsed.Verb == Verb.Edit:
                Clear(parsed, flag);
                return 0;
            case "--linear" or "--name" or "--link" or "--state" or "--note" or "--target" or "--version" when Takes(parsed.Verb, flag):
                return i + 1 < rest.Length ? Valued(parsed, flag, rest[i + 1]) : SindriErrors.InvalidInput.With($"{flag} needs a value: {Usage}");
            case var other when other.StartsWith("--", StringComparison.Ordinal):
                return SindriErrors.InvalidInput.With($"'{other}' isn't a flag of this command: {Usage}");
            default:
                words.Add(flag);
                return 0;
        }
    }

    private static bool Takes(Verb verb, string flag) => flag switch
    {
        "--linear" => verb == Verb.Add,
        "--version" => verb != Verb.Add,
        _ => verb is Verb.Add or Verb.Edit,
    };

    private static void Clear(Parsed parsed, string flag)
    {
        switch (flag)
        {
            case "--clear-state":
                parsed.ClearState = true;
                break;
            case "--clear-note":
                parsed.ClearNote = true;
                break;
            default:
                parsed.ClearTarget = true;
                break;
        }
    }

    private static Result<int> Valued(Parsed parsed, string flag, string value)
    {
        var read = flag switch { "--state" or "--target" or "--version" => Typed(parsed, flag, value), _ => null };
        if (read is not null)
        {
            return read;
        }

        switch (flag)
        {
            case "--linear":
                parsed.Linear = value;
                break;
            case "--name":
                parsed.Name = value;
                break;
            case "--link":
                parsed.Links.Add(value);
                break;
            default:
                parsed.Note = value;
                break;
        }

        return 1;
    }

    /// <summary>Reads the flags whose value has a shape; the error, or null when it fitted.</summary>
    private static Error? Typed(Parsed parsed, string flag, string value)
    {
        switch (flag)
        {
            case "--state":
                if (State(value) is not { } state)
                {
                    return SindriErrors.ProjectInvalid.With($"state: '{value}' isn't one of backlog, planned, in-progress, paused, done, canceled");
                }

                parsed.State = state;
                return null;
            case "--target":
                if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var target))
                {
                    return SindriErrors.ProjectInvalid.With($"target: '{value}' isn't a date (YYYY-MM-DD)");
                }

                parsed.Target = target;
                return null;
            default:
                if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var version))
                {
                    return SindriErrors.InvalidInput.With($"--version needs a whole number, not '{value}'");
                }

                parsed.Version = version;
                return null;
        }
    }

    private static ProjectState? State(string word) => word switch
    {
        "backlog" => ProjectState.Backlog,
        "planned" => ProjectState.Planned,
        "in-progress" => ProjectState.InProgress,
        "paused" => ProjectState.Paused,
        "done" => ProjectState.Done,
        "canceled" => ProjectState.Canceled,
        _ => null,
    };

    /// <summary>Checks the plain words the verb took and keeps the project; the error, or null when they fit.</summary>
    private static Error? Shape(Parsed parsed, List<string> words)
    {
        if (parsed.Verb == Verb.Add)
        {
            return words.Count > 0 ? SindriErrors.InvalidInput.With($"'{words[0]}' isn't an argument of project add: {Usage}")
                : (parsed.Linear is null) == (parsed.Name is null) ? SindriErrors.InvalidInput.With("project add needs --linear or --name, one of them")
                : null;
        }

        if (words.Count != 1)
        {
            return SindriErrors.InvalidInput.With($"project {parsed.Verb.ToString().ToLowerInvariant()} needs one project: {Usage}");
        }

        parsed.Project = words[0];
        return parsed.Version is null ? SindriErrors.InvalidInput.With(NeedsVersion) : null;
    }

    internal enum Verb
    {
        None,
        Add,
        Edit,
        Remove,
        Hide,
        Unhide,
    }

    /// <summary>The arguments of one project verb; <see cref="Project"/> is the project of every verb but 'add'.</summary>
    /// <param name="Verb">The verb.</param>
    internal sealed record Parsed(Verb Verb)
    {
        public bool Json { get; set; }

        public bool NoStart { get; set; }

        public string? Linear { get; set; }

        public string? Name { get; set; }

        public List<string> Links { get; } = [];

        public ProjectState? State { get; set; }

        public bool ClearState { get; set; }

        public string? Note { get; set; }

        public bool ClearNote { get; set; }

        public DateOnly? Target { get; set; }

        public bool ClearTarget { get; set; }

        public long? Version { get; set; }

        public string Project { get; set; } = string.Empty;
    }
}
