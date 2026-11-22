using System.Globalization;

namespace Aw.Meta.Host.Commands;

/// <summary>A command's parsed arguments.</summary>
/// <param name="Positionals">The positional arguments.</param>
/// <param name="Values">Option → value.</param>
/// <param name="Flags">The boolean flags given.</param>
internal sealed record CommandLine(IReadOnlyList<string> Positionals, IReadOnlyDictionary<string, string> Values, IReadOnlySet<string> Flags)
{
    internal string? this[string option] => Values.GetValueOrDefault(option);

    /// <summary>The values of the repeatable options, in the order given.</summary>
    internal IReadOnlyDictionary<string, IReadOnlyList<string>> Lists { get; init; } = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

    // Every value of a repeatable option; empty when it was not given.
    internal IReadOnlyList<string> All(string option) => Lists.GetValueOrDefault(option) ?? [];

    // A required option: Parse already guaranteed it is present.
    internal string Value(string option) => Values[option];

    internal string Positional(int index) => Positionals[index];

    internal bool Json => Flags.Contains(CommandSpec.JsonFlag);

    internal bool Has(string flag) => Flags.Contains(flag);

    /// <summary>Parses <paramref name="args"/> against <paramref name="spec"/>: an unknown option, a missing value or a missing required option is a usage error.</summary>
    internal static CommandLine? Parse(IReadOnlyList<string> args, CommandSpec spec, out string problem)
    {
        var (positionals, values, flags) = (new List<string>(), new Dictionary<string, string>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal));
        var lists = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        for (var i = 0; i < args.Count; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                positionals.Add(args[i]);
            }
            else if (spec.Repeated.Contains(args[i][2..], StringComparer.Ordinal))
            {
                if (Repeated(args, ref i, lists) is { } missing)
                {
                    problem = missing;
                    return null;
                }
            }
            else if (Option(args, ref i, spec, values, flags) is { } bad)
            {
                problem = bad;
                return null;
            }
        }

        var line = new CommandLine(positionals, values, flags) { Lists = lists };
        problem = Check(spec, line);
        return problem.Length == 0 ? line : null;
    }

    // A repeatable option at args[i] and its one non-empty value, added to the option's list. Returns the problem, if any.
    private static string? Repeated(IReadOnlyList<string> args, ref int i, Dictionary<string, IReadOnlyList<string>> lists)
    {
        var name = args[i][2..];
        if (i + 1 >= args.Count || args[i + 1].Length == 0 || args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            return $"--{name} needs one non-empty value";
        }

        lists[name] = [.. lists.GetValueOrDefault(name) ?? [], args[++i]];
        return null;
    }

    // One option at args[i] (and its value): a flag, or an option with one non-empty value. Returns the problem, if any.
    private static string? Option(IReadOnlyList<string> args, ref int i, CommandSpec spec, Dictionary<string, string> values, HashSet<string> flags)
    {
        var name = args[i][2..];
        if (name == CommandSpec.JsonFlag || spec.Flags.Contains(name, StringComparer.Ordinal))
        {
            _ = flags.Add(name);
            return null;
        }

        if (!spec.Required.Concat(spec.Optional).Contains(name, StringComparer.Ordinal))
        {
            return $"unknown option --{name}";
        }

        var hasValue = i + 1 < args.Count && args[i + 1].Length > 0 && !args[i + 1].StartsWith("--", StringComparison.Ordinal);
        return hasValue && values.TryAdd(name, args[++i]) ? null : $"--{name} needs one non-empty value";
    }

    private static string Check(CommandSpec spec, CommandLine line)
    {
        if (spec.Required.FirstOrDefault(r => !line.Values.ContainsKey(r)) is { } missing)
        {
            return $"missing --{missing}";
        }

        return line.Positionals.Count == spec.Positionals || (spec.AnyMore && line.Positionals.Count > spec.Positionals)
            ? string.Empty
            : string.Create(CultureInfo.InvariantCulture, $"expected {spec.Positionals} argument(s) before the options, got {line.Positionals.Count}");
    }
}
