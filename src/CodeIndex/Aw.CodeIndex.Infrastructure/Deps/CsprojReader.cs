using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;

namespace Aw.CodeIndex.Infrastructure.Deps;

/// <summary>Reads the dependencies of *.csproj files.</summary>
internal sealed class CsprojReader : IManifestReader
{
    public bool Handles(string path) => path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<DepRecord> Read(string path, string text)
    {
        var root = ManifestXml.Parse(text);
        if (root is null)
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<DepRecord>();
        foreach (var item in ManifestXml.Items(root, "PackageReference"))
        {
            var name = ManifestXml.Attribute(item, "Include");
            if (name.Length == 0 || !seen.Add(name))
            {
                continue;
            }

            var version = ManifestXml.AttributeOrChild(item, "VersionOverride");
            if (version.Length == 0)
            {
                version = ManifestXml.AttributeOrChild(item, "Version");
            }

            var kind = string.Equals(ManifestXml.AttributeOrChild(item, "PrivateAssets"), "all", StringComparison.OrdinalIgnoreCase) ? "dev" : "runtime";
            rows.Add(new DepRecord(path, name, version, kind, ManifestXml.IsConditional(item) ? "conditional" : string.Empty));
            if (rows.Count == CodeIndexRules.MaxDepsPerManifest)
            {
                break;
            }
        }

        return rows;
    }
}
