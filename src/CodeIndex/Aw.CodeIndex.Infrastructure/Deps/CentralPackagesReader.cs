using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;

namespace Aw.CodeIndex.Infrastructure.Deps;

/// <summary>Reads the dependencies of Directory.Packages.props files.</summary>
internal sealed class CentralPackagesReader : IManifestReader
{
    private static readonly HashSet<string> _itemNames = new(["PackageVersion", "GlobalPackageReference"], StringComparer.Ordinal);

    public bool Handles(string path) => ManifestXml.NameIs(path, "Directory.Packages.props");

    public IReadOnlyList<DepRecord> Read(string path, string text)
    {
        var root = ManifestXml.Parse(text);
        if (root is null)
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<DepRecord>();
        var items = root.Descendants()
            .Where(e => _itemNames.Contains(e.Name.LocalName));
        foreach (var item in items)
        {
            var name = ManifestXml.Attribute(item, "Include");
            if (name.Length == 0 || !seen.Add($"{item.Name.LocalName}:{name}"))
            {
                continue;
            }

            var kind = item.Name.LocalName == "PackageVersion" ? "central" : "global";
            rows.Add(new DepRecord(path, name, ManifestXml.AttributeOrChild(item, "Version"), kind, ManifestXml.IsConditional(item) ? "conditional" : string.Empty));
            if (rows.Count == CodeIndexRules.MaxDepsPerManifest)
            {
                break;
            }
        }

        return rows;
    }
}
