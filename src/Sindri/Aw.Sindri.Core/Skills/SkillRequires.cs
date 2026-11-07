namespace Aw.Sindri.Core.Skills;

/// <summary>A skill's requires.yaml (skill-revamp spec §2.5): the bins, agents, MCP servers and skills its text calls.</summary>
/// <param name="Bins">Executables on PATH.</param>
/// <param name="Agents">Agent types the agent home carries.</param>
/// <param name="Mcp">MCP servers registered with the agent.</param>
/// <param name="Skills">Other skills the agent home links.</param>
public sealed record SkillRequires(IReadOnlyList<string> Bins, IReadOnlyList<string> Agents, IReadOnlyList<string> Mcp, IReadOnlyList<string> Skills)
{
    private static readonly char[] _comment = ['#'];

    /// <summary>Nothing required.</summary>
    public static SkillRequires None { get; } = new([], [], [], []);

    /// <summary>Reads the file in the one shape scripts/check-skill-requires.sh accepts: '{}' alone, or 'key: [a, b]' lines; comments and blank lines are ignored.</summary>
    /// <param name="text">The file's text.</param>
    /// <param name="problem">Why it can't be read; null on success.</param>
    /// <returns>The requirements; <see cref="None"/> on a problem.</returns>
    public static SkillRequires Parse(string text, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(text);
        problem = null;
        Dictionary<string, List<string>> lists = new(StringComparer.Ordinal) { ["bins"] = [], ["agents"] = [], ["mcp"] = [], ["skills"] = [] };
        var seen = false;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Split(_comment, 2)[0].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            seen = true;
            if (string.Equals(line, "{}", StringComparison.Ordinal))
            {
                continue;
            }

            var colon = line.IndexOf(':', StringComparison.Ordinal);
            var key = colon < 0 ? string.Empty : line[..colon].Trim();
            var value = colon < 0 ? string.Empty : line[(colon + 1)..].Trim();
            if (!lists.TryGetValue(key, out var list) || !value.StartsWith('[') || !value.EndsWith(']'))
            {
                problem = $"unreadable line '{line}' (expected 'bins|agents|mcp|skills: [a, b]')";
                return None;
            }

            list.AddRange(value[1..^1].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        }

        if (!seen)
        {
            problem = "the file is empty (write {} for no dependencies)";
            return None;
        }

        return new SkillRequires(lists["bins"], lists["agents"], lists["mcp"], lists["skills"]);
    }
}
