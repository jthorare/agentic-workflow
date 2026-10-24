using System.Globalization;

namespace Aw.UiDriver.Cli;

/// <summary>
/// Strictly parsed arguments: 'aw-ui-driver [--dir D] [--connect-ms N] &lt;verb&gt; [flags]'. An unknown flag, a flag without its value, a repeated flag or
/// a number that is none is a usage error (null from <see cref="Parse"/>). A value never starts with '--'.
/// </summary>
internal sealed class CliArguments
{
    internal const string Usage =
        "aw-ui-driver [--dir D] <ready|tree|search|props|click|type|key|scroll|wait-for|screenshot|window|action|call> [flags]\n"
        + "  selector flags: --node-id --automation-id --name --type --text --contains --index\n"
        + "  tree --depth N · search --limit N · type --value TEXT [--replace] · key --chord Ctrl+K · scroll --dx N --dy N\n"
        + "  wait-for --state exists|visible|hidden|gone|enabled|disabled|textEquals|textContains [--expect-text T] [--wait-ms N]\n"
        + "  screenshot [--out file.png] · window [--width N --height N] · action --do invoke|toggle|select|expand|collapse|focus\n"
        + "  ready [--wait-ms N] · call --path <verb> --json '<request json>'";

    private static readonly string[] _valueFlags =
    [
        "--dir", "--node-id", "--automation-id", "--name", "--type", "--text", "--contains", "--index", "--depth", "--limit", "--value", "--chord", "--dx", "--dy",
        "--state", "--expect-text", "--wait-ms", "--out", "--width", "--height", "--do", "--path", "--json",
    ];

    private static readonly string[] _switches = ["--replace"];

    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    private readonly HashSet<string> _on = new(StringComparer.Ordinal);

    private CliArguments(string verb)
    {
        Verb = verb;
    }

    /// <summary>The verb.</summary>
    internal string Verb { get; }

    internal static CliArguments? Parse(IReadOnlyList<string> args)
    {
        var verbAt = -1;
        for (var i = 0; i < args.Count && verbAt < 0; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                verbAt = i;
            }
            else if (_valueFlags.Contains(args[i], StringComparer.Ordinal))
            {
                i++;   // its value (checked below)
            }
        }

        if (verbAt < 0)
        {
            return null;
        }

        var parsed = new CliArguments(args[verbAt]);
        for (var i = 0; i < args.Count; i++)
        {
            if (i == verbAt)
            {
                continue;
            }

            if (!parsed.Take(args, ref i))
            {
                return null;
            }
        }

        return parsed;
    }

    internal string? Value(string flag) => _values.GetValueOrDefault(flag);

    internal bool Has(string flag) => _on.Contains(flag);

    /// <summary>The flag as a whole number, null when absent.</summary>
    /// <param name="flag">The flag.</param>
    /// <returns>The number, or null.</returns>
    internal int? Number(string flag) => Value(flag) is { } text ? int.Parse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture) : null;

    /// <summary>The flag as a number with a fraction allowed, null when absent.</summary>
    /// <param name="flag">The flag.</param>
    /// <returns>The number, or null.</returns>
    internal double? Real(string flag) => Value(flag) is { } text ? double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture) : null;

    private bool Take(IReadOnlyList<string> args, ref int i)
    {
        var flag = args[i];
        if (_switches.Contains(flag, StringComparer.Ordinal))
        {
            return _on.Add(flag);
        }

        if (!_valueFlags.Contains(flag, StringComparer.Ordinal) || _values.ContainsKey(flag) || i + 1 >= args.Count)
        {
            return false;
        }

        var value = args[++i];
        if (value.StartsWith("--", StringComparison.Ordinal) || !IsNumberFlagOk(flag, value))
        {
            return false;
        }

        _values[flag] = value;
        return true;
    }

    private static bool IsNumberFlagOk(string flag, string value) => flag switch
    {
        "--index" or "--depth" or "--limit" or "--wait-ms" or "--width" or "--height" => int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _),
        "--dx" or "--dy" => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _),
        _ => true,
    };
}
