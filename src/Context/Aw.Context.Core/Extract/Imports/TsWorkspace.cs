using System.Text.Json;

namespace Aw.Context.Core.Extract.Imports;

/// <summary>Reads the repo's package.json files and tsconfig 'paths' aliases from the file set.</summary>
internal static class TsWorkspace
{
    private static readonly JsonDocumentOptions _lenient = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    private static readonly string[] _targetKeys = [".", "types", "import", "default", "require"];

    internal static IReadOnlyList<PackageRoot> Packages(IEnumerable<SourceFile> files) =>
        [.. files.Where(f => Named(f.Path, "package.json")).Select(Package).OfType<PackageRoot>()];

    internal static IReadOnlyList<PathAlias> Aliases(IEnumerable<SourceFile> files) =>
        [.. files.Where(f => Named(f.Path, "tsconfig.json") || Named(f.Path, "jsconfig.json")).SelectMany(Config)];

    private static bool Named(string path, string name) => path == name || path.EndsWith("/" + name, StringComparison.Ordinal);

    private static PackageRoot? Package(SourceFile file)
    {
        using var doc = Parse(file.Text);
        if (doc is null || doc.RootElement.ValueKind != JsonValueKind.Object || Text(doc.RootElement, "name") is not { Length: > 0 } name)
        {
            return null;
        }

        var dir = RepoPath.Dir(file.Path);
        var root = doc.RootElement;
        var entries = new[] { root.TryGetProperty("exports", out var exports) ? Target(exports) : null, Text(root, "types"), Text(root, "main") }
            .OfType<string>().Select(e => RepoPath.Join(dir, e)).OfType<string>().ToList();
        return new PackageRoot(name, dir, entries);
    }

    private static string? Target(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString();
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return _targetKeys.Where(k => element.TryGetProperty(k, out _)).Select(k => Target(element.GetProperty(k))).FirstOrDefault(t => t is not null);
    }

    private static IEnumerable<PathAlias> Config(SourceFile file)
    {
        using var doc = Parse(file.Text);
        if (doc is null || !doc.RootElement.TryGetProperty("compilerOptions", out var options) || options.ValueKind != JsonValueKind.Object
            || !options.TryGetProperty("paths", out var paths) || paths.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var scope = RepoPath.Dir(file.Path);
        var baseDir = RepoPath.Join(scope, Text(options, "baseUrl") ?? ".");
        return baseDir is null ? [] : [.. paths.EnumerateObject().Select(p => Alias(scope, baseDir, p))];
    }

    private static PathAlias Alias(string scope, string baseDir, JsonProperty pattern)
    {
        var star = pattern.Name.IndexOf('*', StringComparison.Ordinal);
        var targets = pattern.Value.ValueKind != JsonValueKind.Array ? []
            : pattern.Value.EnumerateArray().Where(t => t.ValueKind == JsonValueKind.String).Select(t => RepoPath.Join(baseDir, t.GetString() ?? "")).OfType<string>().ToList();
        return star < 0
            ? new PathAlias(scope, pattern.Name, Suffix: "", Wild: false, targets)
            : new PathAlias(scope, pattern.Name[..star], pattern.Name[(star + 1)..], Wild: true, targets);
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static JsonDocument? Parse(string text)
    {
        try
        {
            return JsonDocument.Parse(text, _lenient);
        }
        catch (JsonException)
        {
            return null;   // why: a config the repo itself can't parse gives no aliases
        }
    }
}
