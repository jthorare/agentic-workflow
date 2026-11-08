using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Host.Commands;

/// <summary>The arguments of 'sindri memory &lt;verb&gt;': the verb, its operands and the options it takes.</summary>
/// <param name="Verb">list, create, search, write or forget.</param>
/// <param name="Name">The memory's name; empty for list.</param>
/// <param name="Text">The query (search) or the fact (write); empty otherwise.</param>
/// <param name="Description">--description.</param>
/// <param name="Level">--level.</param>
/// <param name="Limit">--limit.</param>
/// <param name="Fact">--fact.</param>
/// <param name="Tag">--tag of forget (the last one typed).</param>
/// <param name="Expired">--expired.</param>
/// <param name="Json">--json.</param>
/// <param name="ExpiresAt">--expires-at.</param>
/// <param name="Tags">Every --tag of write.</param>
internal sealed record MemoryVerbArguments(string Verb, string Name, string Text, string? Description, string? Level, string? Limit, string? Fact, string? Tag, bool Expired, bool Json, DateTimeOffset? ExpiresAt, IReadOnlyList<string> Tags)
{
    private static readonly Dictionary<string, (int Operands, string[] Options)> _verbs = new(StringComparer.Ordinal)
    {
        ["list"] = (0, []),
        ["create"] = (1, ["--description"]),
        ["search"] = (2, ["--level", "--limit"]),
        ["write"] = (2, ["--tag", "--expires-at"]),
        ["forget"] = (1, ["--fact", "--tag"]),
    };

    /// <summary>Reads the arguments; args[0] is 'memory'.</summary>
    /// <param name="args">The command line.</param>
    /// <returns>The arguments, or SND-CLI-003.</returns>
    internal static Result<MemoryVerbArguments> Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length < 2 || !_verbs.TryGetValue(args[1], out var shape))
        {
            return SindriErrors.InvalidInput.With("sindri memory takes list, create, search, write or forget");
        }

        var verb = args[1];
        var scanned = Scan(args, verb, shape.Options);
        if (scanned.IsError)
        {
            return scanned.Error;
        }

        var (operands, options, json, expired) = scanned.Value;
        return operands.Count != shape.Operands
            ? SindriErrors.InvalidInput.With($"sindri memory {verb} takes {shape.Operands.ToString(CultureInfo.InvariantCulture)} operand(s): {Usage(verb)}")
            : Build(verb, operands, options, json, expired);
    }

    private static Result<(List<string> Operands, List<(string Key, string Value)> Options, bool Json, bool Expired)> Scan(string[] args, string verb, string[] allowed)
    {
        var operands = new List<string>();
        var options = new List<(string Key, string Value)>();
        for (var i = 2; i < args.Length; i++)
        {
            if (allowed.Contains(args[i], StringComparer.Ordinal))
            {
                if (i + 1 >= args.Length)
                {
                    return SindriErrors.InvalidInput.With($"{args[i]} needs a value");
                }

                options.Add((args[i], args[++i]));
            }
            else if (string.Equals(args[i], "--json", StringComparison.Ordinal) || (verb == "forget" && string.Equals(args[i], "--expired", StringComparison.Ordinal)))
            {
                // flags carry no value; they are read back from args below
            }
            else if (args[i].StartsWith("--", StringComparison.Ordinal))
            {
                return SindriErrors.InvalidInput.With($"sindri memory {verb} has no option {args[i]}");
            }
            else
            {
                operands.Add(args[i]);
            }
        }

        return (operands, options, args.Contains("--json", StringComparer.Ordinal), verb == "forget" && args.Contains("--expired", StringComparer.Ordinal));
    }

    private static Result<MemoryVerbArguments> Build(string verb, List<string> operands, List<(string Key, string Value)> options, bool json, bool expired)
    {
        string? Last(string key) => options.LastOrDefault(o => o.Key == key).Value;
        DateTimeOffset? expires = null;
        if (Last("--expires-at") is { } stamp)
        {
            if (!DateTimeOffset.TryParse(stamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at))
            {
                return SindriErrors.InvalidInput.With("--expires-at takes an ISO 8601 time, for example 2026-12-01T00:00:00Z");
            }

            expires = at;
        }

        return new MemoryVerbArguments(
            verb,
            operands.Count > 0 ? operands[0] : string.Empty,
            operands.Count > 1 ? operands[1] : string.Empty,
            Last("--description"), Last("--level"), Last("--limit"), Last("--fact"), verb == "forget" ? Last("--tag") : null, expired, json, expires,
            verb == "write" ? [.. options.Where(static o => o.Key == "--tag").Select(static o => o.Value)] : []);
    }

    private static string Usage(string verb) => verb switch
    {
        "list" => "sindri memory list",
        "create" => "sindri memory create <name> [--description <text>]",
        "search" => "sindri memory search <name> <query> [--level loose|balanced|strict] [--limit <n>]",
        "write" => "sindri memory write <name> <text> [--tag <tag>] [--expires-at <time>]",
        _ => "sindri memory forget <name> --fact <id> | --tag <tag> | --expired",
    };
}
