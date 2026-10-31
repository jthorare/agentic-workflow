using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Host.Commands;

/// <summary>Reads the arguments of 'context page set|remove' (pages spec §3.6, "CLI") into one <see cref="Parsed"/>, or the usage error. An '--action' that doesn't parse is SND-CONTEXT-256.</summary>
internal static class ContextPageArguments
{
    internal const string Usage = "context page set <name> [--id ID] [--repo R] [--surface KEY] [--action \"Name[=KEY][->c1,c2]\"]... [--if-hash H] [--dry-run] [--detach] [--no-start] [--json] | context page set --from-file <pages.yaml> [--if-hash H] [--dry-run] [--detach] [--no-start] [--json] | context page remove <id> [--if-hash H] [--detach] [--no-start] [--json]";

    internal enum Verb
    {
        Set,
        Remove,
    }

    internal static Result<Parsed> Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var verb = args switch
        {
            ["page", "set", ..] => Verb.Set,
            ["page", "remove", ..] => Verb.Remove,
            _ => (Verb?)null,
        };
        if (verb is not { } known)
        {
            return SindriErrors.UnknownVerb.With($"'context {string.Join(' ', args)}' is not a page write command: {Usage}");
        }

        var parsed = new Parsed(known);
        var words = new List<string>();
        var rest = args[2..];
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

    /// <summary>Reads one '--action "Name[=KEY][->c1,c2]"'.</summary>
    /// <param name="text">The flag's value.</param>
    /// <returns>The action (its id is left for the job), or SND-CONTEXT-256 naming the flag's value.</returns>
    internal static Result<PageActionSettings> Action(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var arrow = text.IndexOf("->", StringComparison.Ordinal);
        var head = arrow < 0 ? text : text[..arrow];
        var equals = head.IndexOf('=', StringComparison.Ordinal);
        var name = (equals < 0 ? head : head[..equals]).Trim();
        var key = equals < 0 ? null : head[(equals + 1)..].Trim();
        var concepts = arrow < 0 ? null : text[(arrow + 2)..].Split(',', StringSplitOptions.TrimEntries).ToList();
        var problem = Problem(name, key, concepts);
        return problem is not null
            ? ContextErrors.PageActionSpecInvalid.With($"--action \"{text}\": {problem} (fix: --action \"Name=action:Key->concept-a,concept-b\")")
            : new PageActionSettings(Name: name, Action: key, Concepts: concepts);
    }

    private static string? Problem(string name, string? key, List<string>? concepts) =>
        name.Length == 0 ? "no name before '=' or '->'"
        : name.Contains(',', StringComparison.Ordinal) ? "a name has no ',' in it"
        : key is not null && key.Length == 0 ? "no key after '='"
        : key is not null && key.Contains(',', StringComparison.Ordinal) ? "a key has no ',' in it"
        : concepts is not null && (concepts.Count == 0 || concepts.Exists(static c => c.Length == 0)) ? "no concept after '->'"
        : null;

    private static readonly Verb[] _both = [Verb.Set, Verb.Remove];
    private static readonly Verb[] _set = [Verb.Set];

    private static readonly Dictionary<string, (Verb[] Verbs, Action<Parsed> Turn)> _switches = new(StringComparer.Ordinal)
    {
        ["--json"] = (_both, static p => p.Json = true),
        ["--detach"] = (_both, static p => p.Detach = true),
        ["--no-start"] = (_both, static p => p.NoStart = true),
        ["--dry-run"] = (_set, static p => p.DryRun = true),
    };

    private static readonly Dictionary<string, (Verb[] Verbs, Func<Parsed, string, Error?> Take)> _valued = new(StringComparer.Ordinal)
    {
        ["--id"] = (_set, TakeId),
        ["--repo"] = (_set, TakeRepo),
        ["--surface"] = (_set, TakeSurface),
        ["--action"] = (_set, TakeAction),
        ["--from-file"] = (_set, TakeFile),
        ["--if-hash"] = (_both, TakeHash),
    };

    private static Result<int> Flag(Parsed parsed, string[] rest, int i, List<string> words)
    {
        var flag = rest[i];
        if (_switches.TryGetValue(flag, out var on) && on.Verbs.Contains(parsed.Verb))
        {
            on.Turn(parsed);
            return 0;
        }

        if (_valued.TryGetValue(flag, out var valued) && valued.Verbs.Contains(parsed.Verb))
        {
            if (i + 1 >= rest.Length || rest[i + 1].StartsWith("--", StringComparison.Ordinal))   // the next flag is not this flag's value
            {
                return SindriErrors.InvalidInput.With($"{flag} needs a value: {Usage}");
            }

            return valued.Take(parsed, rest[i + 1]) is { } refused ? refused : 1;
        }

        if (flag.StartsWith("--", StringComparison.Ordinal))
        {
            return SindriErrors.InvalidInput.With($"'{flag}' isn't a flag of this command: {Usage}");
        }

        words.Add(flag);
        return 0;
    }

    private static Error? TakeId(Parsed parsed, string value)
    {
        if (!IsId(value) || !PagePinId.TryFrom(value, out var id))
        {
            return SindriErrors.InvalidInput.With($"'{value}' is not a page id (1 to 64 lower-case letters, digits, '.' or '-')");
        }

        parsed.Id = id;
        return null;
    }

    private static Error? TakeRepo(Parsed parsed, string value)
    {
        parsed.Repo = value;
        return null;
    }

    private static Error? TakeSurface(Parsed parsed, string value)
    {
        parsed.Surface = value;
        return null;
    }

    private static Error? TakeFile(Parsed parsed, string value)
    {
        parsed.FromFile = value;
        return null;
    }

    private static Error? TakeAction(Parsed parsed, string value)
    {
        var action = Action(value);
        if (action.IsError)
        {
            return action.Error;
        }

        parsed.Actions.Add(action.Value);
        return null;
    }

    private static Error? TakeHash(Parsed parsed, string value)
    {
        if (!ProfileHash.TryFrom(value, out var hash))
        {
            return SindriErrors.InvalidInput.With($"'{value}' is not a profile hash (64 hex digits)");
        }

        parsed.IfHash = hash;
        return null;
    }

    // The words and flags that don't go together: null when the line is whole.
    private static Error? Shape(Parsed parsed, List<string> words)
    {
        if (parsed.Verb == Verb.Remove)
        {
            return words.Count != 1 || !IsId(words[0]) || !PagePinId.TryFrom(words[0], out var id)
                ? SindriErrors.InvalidInput.With($"'context page remove' takes one page id: {Usage}")
                : With(parsed, id);
        }

        if (parsed.FromFile is not null)
        {
            return words.Count > 0 || PageFlagsGiven(parsed)
                ? SindriErrors.InvalidInput.With($"--from-file takes the file only, not a name or page flags: {Usage}")
                : null;
        }

        parsed.Name = words.Count == 1 ? words[0] : string.Empty;
        return words.Count == 1 ? null : SindriErrors.InvalidInput.With($"'context page set' takes one name (quote it) or --from-file: {Usage}");
    }

    // The shape the build and the profile reader give a page id; PagePinId alone only bounds the length.
    private static bool IsId(string value) => PageId.TryFrom(value, out _);

    private static bool PageFlagsGiven(Parsed parsed) => parsed.Id is not null || parsed.Repo is not null || parsed.Surface is not null || parsed.Actions.Count > 0;

    private static Error? With(Parsed parsed, PagePinId id)
    {
        parsed.Id = id;
        return null;
    }

    /// <summary>What the command line said.</summary>
    internal sealed class Parsed
    {
        internal Parsed(Verb verb)
        {
            Verb = verb;
        }

        internal Verb Verb { get; }

        internal string Name { get; set; } = string.Empty;

        internal PagePinId? Id { get; set; }

        internal string? Repo { get; set; }

        internal string? Surface { get; set; }

        internal List<PageActionSettings> Actions { get; } = [];

        internal string? FromFile { get; set; }

        internal ProfileHash? IfHash { get; set; }

        internal bool DryRun { get; set; }

        internal bool Detach { get; set; }

        internal bool NoStart { get; set; }

        internal bool Json { get; set; }
    }
}
