using System.Globalization;

namespace Aw.Forge.Host.Commands;

/// <summary>A command's parsed arguments. Paths are left as given; the command resolves them.</summary>
/// <param name="Positionals">The positional arguments.</param>
/// <param name="Values">Option → value.</param>
/// <param name="Flags">The boolean flags given.</param>
/// <param name="Command">The argv after "--".</param>
internal sealed record CommandLine(IReadOnlyList<string> Positionals, IReadOnlyDictionary<string, string> Values, IReadOnlySet<string> Flags, IReadOnlyList<string> Command)
{
    // Parse guarantees every required option is present.
    internal string Need(string option) => Values[option];

    internal string? this[string option] => Values.GetValueOrDefault(option);

    internal string StateDirectory => Path.GetFullPath(Values["state"]);

    internal string WorkingDirectory => Path.GetFullPath(Values.GetValueOrDefault("cwd") ?? Directory.GetCurrentDirectory());

    internal string FullPath(string option) => Path.GetFullPath(Values[option]);

    /// <summary>Parses <paramref name="args"/> against <paramref name="spec"/>: an unknown option, a missing value or a missing required option is a usage error.</summary>
    internal static CommandLine? Parse(IReadOnlyList<string> args, CommandSpec spec, out string problem)
    {
        var (positionals, values, flags) = (new List<string>(), new Dictionary<string, string>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal));
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] == "--")
            {
                return spec.TakesCommand ? Finish(spec, new CommandLine(positionals, values, flags, [.. args.Skip(i + 1)]), out problem) : Fail("unexpected --", out problem);
            }

            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                positionals.Add(args[i]);
                continue;
            }

            if (Option(args, ref i, spec, values, flags) is { } bad)
            {
                return Fail(bad, out problem);
            }
        }

        return Finish(spec, new CommandLine(positionals, values, flags, []), out problem);
    }

    // One option at args[i] (and its value): a flag, or an option with one non-empty value. Returns the problem, if any.
    private static string? Option(IReadOnlyList<string> args, ref int i, CommandSpec spec, Dictionary<string, string> values, HashSet<string> flags)
    {
        var name = args[i][2..];
        if (spec.Flags.Contains(name, StringComparer.Ordinal))
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

    private static CommandLine? Finish(CommandSpec spec, CommandLine line, out string problem)
    {
        if (spec.Required.FirstOrDefault(r => !line.Values.ContainsKey(r)) is { } missing)
        {
            return Fail($"missing --{missing}", out problem);
        }

        if (line.Positionals.Count != spec.Positionals)
        {
            return Fail(string.Create(CultureInfo.InvariantCulture, $"expected {spec.Positionals} argument(s) before the options, got {line.Positionals.Count}"), out problem);
        }

        if (spec.TakesCommand && line.Command.Count == 0)
        {
            return Fail("missing the command after --", out problem);
        }

        problem = string.Empty;
        return line;
    }

    private static CommandLine? Fail(string reason, out string problem)
    {
        problem = reason;
        return null;
    }
}
