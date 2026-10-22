using System.Text.Json;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;

namespace Aw.CodeIndex.Infrastructure.Deps;

/// <summary>Reads the dependencies of package.json files.</summary>
internal sealed class PackageJsonReader : IManifestReader
{
    private static readonly (string Section, string Kind)[] _sections =
    [
        ("dependencies", "runtime"),
        ("devDependencies", "dev"),
        ("peerDependencies", "peer"),
        ("optionalDependencies", "optional"),
    ];

    private static readonly string[] _urlPrefixes = ["git+", "git:", "github:", "gitlab:", "bitbucket:", "http:", "https:", "ssh:"];

    public bool Handles(string path) => ManifestXml.NameIs(path, "package.json");

    public IReadOnlyList<DepRecord> Read(string path, string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.ValueKind == JsonValueKind.Object ? Rows(path, document.RootElement) : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static List<DepRecord> Rows(string path, JsonElement root)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var rows = new List<DepRecord>();
        foreach (var (section, kind) in _sections)
        {
            if (!root.TryGetProperty(section, out var block) || block.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var property in block.EnumerateObject())
            {
                if (property.Name.Length == 0 || property.Value.ValueKind != JsonValueKind.String || !seen.Add(property.Name))
                {
                    continue;
                }

                var version = property.Value.GetString() ?? string.Empty;
                rows.Add(new DepRecord(path, property.Name, version, kind, Tags(version)));
                if (rows.Count == CodeIndexRules.MaxDepsPerManifest)
                {
                    return rows;
                }
            }
        }

        return rows;
    }

    private static string Tags(string version) => version switch
    {
        _ when version.StartsWith("workspace:", StringComparison.Ordinal) => "workspace",
        _ when version.StartsWith("file:", StringComparison.Ordinal) || version.StartsWith("link:", StringComparison.Ordinal) => "local",
        _ when _urlPrefixes.Any(p => version.StartsWith(p, StringComparison.Ordinal)) => "url",
        _ => string.Empty,
    };
}
