using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Host.Commands;

/// <summary>Reads the arguments of 'context pin set|remove|list' (concept pins spec §3.6, "CLI and agents") into one <see cref="Parsed"/>, or the usage error. A flag left out is null (keep the stored value), a given list replaces it and '--clear-*' empties it.</summary>
internal static class ContextPinArguments
{
    internal const string Usage = "context pin set <name> [--id ID] [--summary T] [--alias W]... [--feature K]... [--clear-aliases] [--clear-features] [--if-hash H] [--detach] [--no-start] [--json] | context pin set --from-file <list.txt> [--if-hash H] [--detach] [--no-start] [--json] | context pin remove <id> [--with-folder-rules] [--if-hash H] [--detach] [--no-start] [--json] | context pin list [--json]";

    internal enum Verb
    {
        Set,
        Remove,
        List,
    }

    internal static Result<Parsed> Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var verb = args switch
        {
            ["pin", "set", ..] => Verb.Set,
            ["pin", "remove", ..] => Verb.Remove,
            ["pin", "list", ..] => Verb.List,
            _ => (Verb?)null,
        };
        if (verb is not { } known)
        {
            return SindriErrors.UnknownVerb.With($"'context {string.Join(' ', args)}' is not a pin command: {Usage}");
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

    private static readonly Verb[] _all = [Verb.Set, Verb.Remove, Verb.List];
    private static readonly Verb[] _writes = [Verb.Set, Verb.Remove];
    private static readonly Verb[] _set = [Verb.Set];
    private static readonly Verb[] _remove = [Verb.Remove];

    private static readonly Dictionary<string, (Verb[] Verbs, Action<Parsed> Turn)> _switches = new(StringComparer.Ordinal)
    {
        ["--json"] = (_all, static p => p.Json = true),
        ["--detach"] = (_writes, static p => p.Detach = true),
        ["--no-start"] = (_writes, static p => p.NoStart = true),
        ["--clear-aliases"] = (_set, static p => p.ClearAliases = true),
        ["--clear-features"] = (_set, static p => p.ClearFeatures = true),
        ["--with-folder-rules"] = (_remove, static p => p.WithFolderRules = true),
    };

    private static readonly Dictionary<string, (Verb[] Verbs, Func<Parsed, string, Error?> Take)> _valued = new(StringComparer.Ordinal)
    {
        ["--id"] = (_set, TakeId),
        ["--summary"] = (_set, TakeSummary),
        ["--alias"] = (_set, TakeAlias),
        ["--feature"] = (_set, TakeFeature),
        ["--from-file"] = (_set, TakeFile),
        ["--if-hash"] = (_writes, TakeHash),
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
            if (i + 1 >= rest.Length)
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

    private static Error? TakeSummary(Parsed parsed, string value)
    {
        parsed.Summary = value;
        return null;
    }

    private static Error? TakeAlias(Parsed parsed, string value)
    {
        (parsed.Aliases ??= []).Add(value);
        return null;
    }

    private static Error? TakeFeature(Parsed parsed, string value)
    {
        (parsed.Features ??= []).Add(value);
        return null;
    }

    private static Error? TakeFile(Parsed parsed, string value)
    {
        parsed.FromFile = value;
        return null;
    }

    private static Error? TakeId(Parsed parsed, string value)
    {
        if (!ConceptPinId.TryFrom(value, out var id))
        {
            return SindriErrors.InvalidInput.With($"'{value}' is not a pin id (1 to 64 characters)");
        }

        parsed.Id = id;
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
    private static Error? Shape(Parsed parsed, List<string> words) => parsed.Verb switch
    {
        Verb.Set when parsed.FromFile is not null => FromFileShape(parsed, words),
        Verb.Set => SetShape(parsed, words),
        Verb.Remove => RemoveShape(parsed, words),
        Verb.List => words.Count == 0 ? null : SindriErrors.InvalidInput.With($"'context pin list' takes no arguments: {Usage}"),
        _ => null,
    };

    private static Error? FromFileShape(Parsed parsed, List<string> words) =>
        words.Count > 0 || parsed.Id is not null || parsed.Summary is not null || parsed.Aliases is not null || parsed.Features is not null || parsed.ClearAliases || parsed.ClearFeatures
            ? SindriErrors.InvalidInput.With($"--from-file takes the list only, not a name or pin flags: {Usage}")
            : null;

    private static Error? SetShape(Parsed parsed, List<string> words)
    {
        if (words.Count != 1)
        {
            return SindriErrors.InvalidInput.With($"'context pin set' takes one name (quote it) or --from-file: {Usage}");
        }

        parsed.Name = words[0];
        return (parsed.ClearAliases && parsed.Aliases is not null) || (parsed.ClearFeatures && parsed.Features is not null)
            ? SindriErrors.InvalidInput.With($"--clear-aliases and --alias (or --clear-features and --feature) say opposite things: {Usage}")
            : null;
    }

    private static Error? RemoveShape(Parsed parsed, List<string> words)
    {
        if (words.Count != 1 || !ConceptPinId.TryFrom(words[0], out var id))
        {
            return SindriErrors.InvalidInput.With($"'context pin remove' takes one pin id: {Usage}");
        }

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

        internal ConceptPinId? Id { get; set; }

        internal string? Summary { get; set; }

        internal List<string>? Aliases { get; set; }

        internal List<string>? Features { get; set; }

        internal bool ClearAliases { get; set; }

        internal bool ClearFeatures { get; set; }

        internal string? FromFile { get; set; }

        internal ProfileHash? IfHash { get; set; }

        internal bool WithFolderRules { get; set; }

        internal bool Detach { get; set; }

        internal bool NoStart { get; set; }

        internal bool Json { get; set; }
    }
}
