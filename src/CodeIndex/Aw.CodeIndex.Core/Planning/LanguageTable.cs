namespace Aw.CodeIndex.Core.Planning;

/// <summary>The language word of a source file, by extension (spec §5.3); a path with none of these extensions is not a code file and the index does not store it.</summary>
public static class LanguageTable
{
    private static readonly Dictionary<string, string> _byExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = "csharp",
        [".ts"] = "typescript",
        [".tsx"] = "typescript",
        [".js"] = "javascript",
        [".mjs"] = "javascript",
        [".cjs"] = "javascript",
        [".jsx"] = "javascript",
        [".mts"] = "typescript",
        [".cts"] = "typescript",
        [".py"] = "python",
        [".go"] = "go",
        [".rs"] = "rust",
        [".java"] = "java",
        [".kt"] = "kotlin",
        [".rb"] = "ruby",
        [".php"] = "php",
        [".c"] = "c",
        [".h"] = "c",
        [".cc"] = "cpp",
        [".cpp"] = "cpp",
        [".cxx"] = "cpp",
        [".hpp"] = "cpp",
        [".swift"] = "swift",
        [".scala"] = "scala",
        [".lua"] = "lua",
        [".dart"] = "dart",
    };

    /// <summary>Names the language of a file.</summary>
    /// <param name="path">The repo-relative path.</param>
    /// <returns>The language word ('csharp', 'typescript', ...), or null for a file that is not source code.</returns>
    public static string? Of(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var name = path[(path.LastIndexOf('/') + 1)..];
        var dot = name.LastIndexOf('.');
        return dot < 0 ? null : _byExtension.GetValueOrDefault(name[dot..]);
    }
}
