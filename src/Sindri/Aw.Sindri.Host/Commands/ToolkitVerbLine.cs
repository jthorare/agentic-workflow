using System.Globalization;

namespace Aw.Sindri.Host.Commands;

/// <summary>One parsed 'sindri toolkit …' line: the verb, its one name, and the flags it may carry.</summary>
/// <param name="Verb">list, create, build, start, stop, pause, resume or calls.</param>
/// <param name="Name">The toolkit name; null for list.</param>
/// <param name="Json">--json.</param>
/// <param name="Detach">--detach (create and build).</param>
/// <param name="Tool">--tool (create): the first tool's name.</param>
/// <param name="Needs">--needs (create): the generator's needs list.</param>
/// <param name="Limit">--limit (calls): the most calls to print.</param>
internal sealed record ToolkitVerbLine(string Verb, string? Name, bool Json, bool Detach, string? Tool, string? Needs, int? Limit)
{
    private static readonly string[] _valueFlags = ["--tool", "--needs", "--limit"];

    private static readonly string[] _verbs = ["list", "create", "build", "start", "stop", "pause", "resume", "calls"];

    /// <summary>Reads the words after 'toolkit'.</summary>
    /// <param name="words">The words.</param>
    /// <returns>The line, or null when it isn't one of the verbs' forms.</returns>
    internal static ToolkitVerbLine? Parse(IReadOnlyList<string> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        return words.Count > 0 && _verbs.Contains(words[0], StringComparer.Ordinal) && Split([.. words.Skip(1)]) is { } split ? Build(words[0], split.Names, split.Flags) : null;
    }

    private static ToolkitVerbLine? Build(string verb, List<string> names, Dictionary<string, string?> flags)
    {
        var json = flags.Remove("--json");
        var detach = flags.Remove("--detach");
        var limit = ReadLimit(flags);
        var line = new ToolkitVerbLine(verb, names.Count == 1 ? names[0] : null, json, detach, flags.Remove("--tool", out var tool) ? tool : null, flags.Remove("--needs", out var needs) ? needs : null, limit);
        return flags.Count == 0 && limit != 0 && line.Fits(names.Count) ? line : null;
    }

    // null: no --limit; 0: a --limit that isn't a whole number of at least 1.
    private static int? ReadLimit(Dictionary<string, string?> flags)
    {
        if (!flags.Remove("--limit", out var text))
        {
            return null;
        }

        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed >= 1 ? parsed : 0;
    }

    // Each verb takes its own words and flags and no others.
    private bool Fits(int names) => Verb switch
    {
        "list" => names == 0 && Alone(detach: false, tool: false, needs: false, limit: false),
        "create" => names == 1 && Tool is not null && Alone(detach: true, tool: true, needs: true, limit: false),
        "build" => names == 1 && Alone(detach: true, tool: false, needs: false, limit: false),
        "calls" => names == 1 && Alone(detach: false, tool: false, needs: false, limit: true),
        _ => names == 1 && Alone(detach: false, tool: false, needs: false, limit: false),
    };

    private bool Alone(bool detach, bool tool, bool needs, bool limit) =>
        (detach || !Detach) && (tool || Tool is null) && (needs || Needs is null) && (limit || Limit is null);

    // Words that aren't flags are names; a flag that takes a value takes the next word. A value flag with no value, or a repeated flag, is a bad line (null names).
    private static (List<string> Names, Dictionary<string, string?> Flags)? Split(List<string> words)
    {
        var names = new List<string>();
        var flags = new Dictionary<string, string?>(StringComparer.Ordinal);
        for (var i = 0; i < words.Count; i++)
        {
            var word = words[i];
            if (!word.StartsWith("--", StringComparison.Ordinal))
            {
                names.Add(word);
                continue;
            }

            var takesValue = _valueFlags.Contains(word, StringComparer.Ordinal);
            if ((takesValue && i + 1 >= words.Count) || !flags.TryAdd(word, takesValue ? words[++i] : null))
            {
                return null;
            }
        }

        return (names, flags);
    }
}
