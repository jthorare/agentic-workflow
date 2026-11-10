using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Host.Commands;

/// <summary>The connection verbs' argument parsing: the verb, its flags and its names (split from ConnectionVerbs.cs for the file length limit).</summary>
internal sealed partial class ConnectionVerbs
{
    private static readonly string[] _verbs = ["add", "list", "show", "test", "rename", "remove", "signin"];
    private static readonly string[] _switchFlags = ["--json", "--no-start"];
    private static readonly string[] _valueFlags = ["--preset", "--kind", "--client-id", "--client-secret-env", "--key-env", "--scope", "--url"];

    private static Result<Parsed> Parse(string[] args)
    {
        var alias = args[0] == "connections";
        var verb = alias || args.Length < 2 ? "list" : args[1];
        if (!_verbs.Contains(verb, StringComparer.Ordinal))
        {
            return SindriErrors.InvalidInput.With("usage: sindri connection add|list|show|test|rename|remove|signin (fix: sindri help)");
        }

        var scanned = Scan([.. args.Skip(alias ? 1 : 2)]);
        if (scanned.IsError)
        {
            return scanned.Error;
        }

        var (values, switches, names) = scanned.Value;
        if (WrongNames(verb, names) is { } wrong)
        {
            return wrong;
        }

        return Build(verb, values, switches, names);
    }

    private static Parsed Build(string verb, Dictionary<string, List<string>> values, HashSet<string> switches, List<string> names)
    {
        string? Last(string flag) => values.TryGetValue(flag, out var given) ? given[^1] : null;
        return new Parsed(verb, names.Count == 0 ? string.Empty : names[0], Last("--preset"), Last("--kind"), Last("--client-id"), Last("--key-env"), Last("--client-secret-env"), values.GetValueOrDefault("--scope") ?? [], switches.Contains("--json"), switches.Contains("--no-start"), Last("--url"), names.Count > 1 ? names[1] : null);
    }

    private static Error? WrongNames(string verb, List<string> names) =>
        string.Equals(verb, "rename", StringComparison.Ordinal)
            ? (names.Count == 2 ? null : SindriErrors.InvalidInput.With("sindri connection rename takes the name (service:label) and the new label"))
            : names.Count > 1 || (!string.Equals(verb, "list", StringComparison.Ordinal) && names.Count == 0)
                ? SindriErrors.InvalidInput.With($"sindri connection {verb} takes exactly one name (service:label)")
                : null;

    private static Result<(Dictionary<string, List<string>> Values, HashSet<string> Switches, List<string> Names)> Scan(string[] rest)
    {
        var values = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var switches = new HashSet<string>(StringComparer.Ordinal);
        var names = new List<string>();
        for (var i = 0; i < rest.Length; i++)
        {
            var word = rest[i];
            if (_switchFlags.Contains(word, StringComparer.Ordinal))
            {
                _ = switches.Add(word);
            }
            else if (_valueFlags.Contains(word, StringComparer.Ordinal))
            {
                if (i + 1 >= rest.Length)
                {
                    return SindriErrors.InvalidInput.With($"{word} needs a value");
                }

                _ = values.TryAdd(word, []);
                values[word].Add(rest[++i]);
            }
            else if (word.StartsWith('-'))
            {
                return SindriErrors.InvalidInput.With($"unknown flag {word} (fix: sindri help)");
            }
            else
            {
                names.Add(word);
            }
        }

        return (values, switches, names);
    }

    private sealed record Parsed(string Verb, string Name, string? Preset, string? Kind, string? Client, string? KeyEnv, string? ClientSecretEnv, IReadOnlyList<string> Scopes, bool Json, bool NoStart, string? Url = null, string? Label = null);
}
