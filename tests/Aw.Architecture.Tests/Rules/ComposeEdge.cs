namespace Aw.Architecture.Tests.Rules;

/// <summary>
/// A deps.yaml compose edge by package root: the Host of <paramref name="HostRoot"/> may call the public Add…()
/// registration methods of <paramref name="TargetRoot"/>'s Features and Infrastructure, and nothing else from them
/// (context spec §4; ARCH-COMPOSE-001).
/// </summary>
/// <param name="HostRoot">The composing package's root, e.g. Aw.Sindri.</param>
/// <param name="TargetRoot">The composed package's root, e.g. Aw.Context.</param>
public sealed record ComposeEdge(string HostRoot, string TargetRoot)
{
    /// <summary>
    /// The compose edges declared in a deps.yaml text, mapped to package roots by projectPrefix. It reads the block
    /// style deps.yaml uses (one "- " item per package or edge, one "key: value" per line); check-deps validates the file.
    /// </summary>
    /// <param name="depsYaml">deps.yaml's text.</param>
    /// <returns>The edges.</returns>
    public static IReadOnlyList<ComposeEdge> Parse(string depsYaml)
    {
        var sections = Sections(depsYaml);
        var prefixes = sections.GetValueOrDefault("packages", [])
            .Where(p => p.ContainsKey("projectPrefix"))
            .ToDictionary(p => p["id"], p => p["projectPrefix"], StringComparer.Ordinal);
        return [.. sections.GetValueOrDefault("edges", [])
            .Where(e => e.GetValueOrDefault("kind") == "compose" && prefixes.ContainsKey(e["from"]) && prefixes.ContainsKey(e["to"]))
            .Select(e => new ComposeEdge(prefixes[e["from"]], prefixes[e["to"]]))];
    }

    // Top-level key → its list items, each item's "key: value" lines (quotes removed).
    private static Dictionary<string, List<Dictionary<string, string>>> Sections(string text)
    {
        var sections = new Dictionary<string, List<Dictionary<string, string>>>(StringComparer.Ordinal);
        List<Dictionary<string, string>>? items = null;
        foreach (var line in text.Split('\n').Select(l => l.TrimEnd('\r')))
        {
            var body = line.TrimStart();
            if (body.Length == 0 || body[0] == '#')
            {
                continue;
            }

            if (body.Length == line.Length)
            {
                items = [];
                sections[line.Split(':')[0]] = items;
                continue;
            }

            if (body.StartsWith("- ", StringComparison.Ordinal))
            {
                items?.Add(new Dictionary<string, string>(StringComparer.Ordinal));
                body = body[2..];
            }

            var colon = body.IndexOf(": ", StringComparison.Ordinal);
            if (items is [.., var item] && colon > 0)
            {
                _ = item.TryAdd(body[..colon], body[(colon + 2)..].Trim().Trim('"', '\''));
            }
        }

        return sections;
    }
}
