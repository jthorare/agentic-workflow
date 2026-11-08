#pragma warning disable MA0182 // why: the graph block registry is not registered in DI yet (EmptyGraphBlockCatalog); remove this when it is
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Infrastructure.Workflow.Vulnerabilities;

/// <summary>The [[package]] tables of a Cargo.lock or poetry.lock: each one's name and version.</summary>
internal static class TomlPackages
{
    private const string Header = "[[package]]";

    /// <summary>Reads the packages.</summary>
    /// <param name="text">The lockfile.</param>
    /// <param name="ecosystem">The ecosystem the packages belong to.</param>
    /// <param name="requireSource">True to skip packages with no 'source' line (Cargo's workspace members).</param>
    /// <returns>The packages.</returns>
    public static List<VulnPackage> Read(string text, string ecosystem, bool requireSource)
    {
        var found = new List<VulnPackage>();
        string? name = null;
        string? version = null;
        var hasSource = false;
        foreach (var raw in text.Split('\n').Append(Header))
        {
            var line = raw.Trim();
            if (line.StartsWith(Header, StringComparison.Ordinal))
            {
                if (name is not null && version is not null && (hasSource || !requireSource))
                {
                    found.Add(new VulnPackage(ecosystem, name, version));
                }

                (name, version, hasSource) = (null, null, false);
                continue;
            }

            name ??= Value(line, "name");
            version ??= Value(line, "version");
            hasSource |= line.StartsWith("source", StringComparison.Ordinal);
        }

        return found;
    }

    private static string? Value(string line, string key)
    {
        var prefix = key + " = \"";
        return line.StartsWith(prefix, StringComparison.Ordinal) && line.EndsWith('"') ? line[prefix.Length..^1] : null;
    }
}
