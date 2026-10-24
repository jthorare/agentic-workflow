using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Context.Infrastructure.Map;

/// <summary>Reads a map file: refuses one inside a git worktree, hashes its bytes, then parses YAML (or JSON) into a <see cref="MapDocument"/> through AwJson.</summary>
internal sealed class MapFileReader(JsonSerializerOptions options) : IMapFileReader
{
    private const long MaxBytes = 256L * 1024 * 1024;

    public async ValueTask<Result<MapFile>> Read(string path, CancellationToken cancellationToken)
    {
        if (path.Any(char.IsControl) || !Path.IsPathRooted(path) || RealPath.Of(path) is not { } full)
        {
            return ContextErrors.MapFileInvalid.With("the map file's path isn't an absolute, resolvable path without control characters; nothing was written (fix: give its full path)");
        }
        if (Worktrees.Containing(full) is { } worktree)
        {
            return ContextErrors.PathInWorktree.With($"{full} is inside the git worktree {worktree} (fix: keep map files outside every repo)");
        }

        byte[] bytes;
        try
        {
            if (new FileInfo(full).Length > MaxBytes)
            {
                return ContextErrors.MapFileInvalid.With($"{full}: over 256 MB; nothing was written");
            }

            bytes = await File.ReadAllBytesAsync(full, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return ContextErrors.MapFileInvalid.With($"{full} can't be read: {e.Message}");
        }

        var parsed = Parse(bytes);
        return parsed.IsError
            ? ContextErrors.MapFileInvalid.With($"{full}: {parsed.Error.Message}; nothing was written (fix: correct the file and import again)")
            : new MapFile(parsed.Value, Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    private Result<MapDocument> Parse(byte[] bytes)
    {
        var text = Encoding.UTF8.GetString(bytes);
        if (text.TrimStart().StartsWith('{'))
        {
            return AwJson.Deserialize<MapDocument>(bytes, options);
        }

        return YamlToJson.TryConvert(text, out var json) is { } problem
            ? KernelErrors.Invalid.With(problem)
            : AwJson.Deserialize<MapDocument>(json, options);
    }
}
