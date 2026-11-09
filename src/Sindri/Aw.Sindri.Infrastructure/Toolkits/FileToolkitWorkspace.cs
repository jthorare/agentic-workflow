using System.Security.Cryptography;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Infrastructure.Toolkits;

/// <summary>A toolkit's folder on disk: the source hash is SHA-256 over every file's relative path and bytes, in path order, leaving out bin, obj and .git; the manifest is toolkit.json in the folder.</summary>
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

    public async ValueTask<Result<string>> ReadManifest(string dir, CancellationToken cancellationToken)
    {
        var path = Path.Combine(dir, "toolkit.json");
        try
        {
            return await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.ToolkitManifestInvalid.With($"{path} can't be read (fix: the toolkit's build writes it with --manifest)");
        }
    }

    private static bool IsSkipped(string relative) => _skipped.Contains(relative.Split('/', '\\')[0], StringComparer.Ordinal);
}
