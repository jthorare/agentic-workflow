using System.Security.Cryptography;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Infrastructure.Toolkits;

/// <summary>A toolkit's folder on disk: the source hash is SHA-256 over every file's relative path and bytes, in path order, leaving out bin, obj and .git; the binary is the newest file named after the toolkit under bin.</summary>
internal sealed class FileToolkitWorkspace : IToolkitWorkspace
{
    private static readonly string[] _skipped = ["bin", "obj", ".git"];

    public async ValueTask<Result<string>> SourceHash(string dir, CancellationToken cancellationToken)
    {
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(dir, f)).Where(static r => !IsSkipped(r)).Order(StringComparer.Ordinal))
            {
                hash.AppendData(Encoding.UTF8.GetBytes(file.Replace('\\', '/') + "\n"));
                hash.AppendData(await File.ReadAllBytesAsync(Path.Combine(dir, file), cancellationToken).ConfigureAwait(false));
            }

            return Convert.ToHexStringLower(hash.GetHashAndReset());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.ToolkitNotBuilt.With($"the source in {dir} can't be read: {e.Message}");
        }
    }

    public Result<string> BinaryOf(string dir, string name) =>
        FindBinary(dir, name) is { } binary
            ? binary
            : SindriErrors.ToolkitManifestInvalid.With($"the build left no binary named {name} under {Path.Combine(dir, "bin")}");

    /// <summary>The newest file named <paramref name="name"/> under the folder's bin, what `dotnet build` or a publish left, or null.</summary>
    /// <param name="dir">The toolkit's folder.</param>
    /// <param name="name">The toolkit name.</param>
    /// <returns>The absolute path, or null.</returns>
    internal static string? FindBinary(string dir, string name)
    {
        var bin = Path.Combine(dir, "bin");
        return Directory.Exists(bin)
            ? Directory.EnumerateFiles(bin, name, SearchOption.AllDirectories).OrderByDescending(static f => File.GetLastWriteTimeUtc(f)).FirstOrDefault()
            : null;
    }

    private static bool IsSkipped(string relative) => _skipped.Contains(relative.Split('/', '\\')[0], StringComparer.Ordinal);
}
