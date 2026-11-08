using System.Text.RegularExpressions;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// Cursor's models (#2110). Reads the output of 'cursor-agent --list-models' (lines "id - Display Name", with a trailing "(current)" or "(default)"),
/// and when that list is unavailable (not logged in, command failed, no line parsed) gives a fixed list of ids from Cursor's public docs.
/// Neither was checked against a logged-in cursor-agent: the live line shape is the best-known one and the fixed list may go stale.
/// </summary>
internal static partial class CursorModels
{
    /// <summary>The ids the CLI documents, cheapest first by the pinned names of the routing spec (D12) then the documented ones. Unverified against a live list.</summary>
    private static readonly (string Slug, string Name)[] _documented =
    [
        ("auto", "Auto"),
        ("gpt-5.6-luna-none-fast", "GPT-5.6 Luna Fast"),
        ("composer-1", "Composer 1"),
        ("gpt-5", "GPT-5"),
        ("sonnet-4.5", "Claude 4.5 Sonnet"),
        ("opus-4.1", "Claude 4.1 Opus"),
    ];

    /// <summary>Parses the live list; null or unparsable text gives the documented list.</summary>
    /// <param name="output">The command's stdout, or null when it failed.</param>
    /// <returns>The models, never empty.</returns>
    public static IReadOnlyList<ModelFacts> Read(string? output)
    {
        var live = Parse(output);
        return live.Count > 0 ? live : Fallback;
    }

    /// <summary>The fixed list, source <see cref="ModelSource.Catalog"/>.</summary>
    public static IReadOnlyList<ModelFacts> Fallback { get; } = [.. _documented.Select(static (m, i) => new ModelFacts(m.Slug, m.Name, ModelSource.Catalog, i))];

    private static List<ModelFacts> Parse(string? output)
    {
        var models = new List<ModelFacts>();
        if (string.IsNullOrWhiteSpace(output))
        {
            return models;
        }

        foreach (var raw in Escapes.Replace(output, string.Empty).Split('\n'))
        {
            if (Row.Match(raw.Trim()) is { Success: true } row && models.TrueForAll(m => !string.Equals(m.Slug, row.Groups["id"].Value, StringComparison.Ordinal)))
            {
                models.Add(new ModelFacts(row.Groups["id"].Value, Suffix.Replace(row.Groups["name"].Value, string.Empty).Trim(), ModelSource.Discovered));
            }
        }

        return models;
    }

    [GeneratedRegex(@"\x1B\[[0-9;]*[A-Za-z]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex Escapes { get; }

    [GeneratedRegex(@"^(?<id>[A-Za-z0-9][A-Za-z0-9._:/@+-]*)\s+-\s+(?<name>\S.*)$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex Row { get; }

    [GeneratedRegex(@"\s*\((?:current|default)(?:,\s*(?:current|default))?\)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex Suffix { get; }
}
