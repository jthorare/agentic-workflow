using System.Text;
using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Review;
using Aw.Context.Infrastructure.Map;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Context.Infrastructure.Review;

/// <summary>Review files: JSON written under the exports folder by temp file and rename at 0600, and YAML or JSON read from outside every git worktree.</summary>
/// <param name="settings">Where the exports folder is.</param>
/// <param name="options">The AwJson options over the review types.</param>
internal sealed class FileReviewFileStore(ContextStoreSettings settings, JsonSerializerOptions options) : IReviewFileStore
{
    private const long MaxBytes = 64L * 1024 * 1024;

    public ValueTask<Result<string>> Write(string? path, string defaultName, ReviewFile file, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        var resolved = ExportTarget.Resolve(settings, path, defaultName);
        if (resolved.IsError)
        {
            return ValueTask.FromResult<Result<string>>(resolved.Error);
        }

        try
        {
            OwnerOnly.EnsureDirectory(settings.Home);
            OwnerOnly.EnsureDirectory(Path.GetFullPath(settings.ExportsDirectory));
            OwnerOnly.EnsureDirectory(Path.GetDirectoryName(resolved.Value)!);   // why: the target is an absolute file path inside the folder
            OwnerOnly.WriteAtomically(resolved.Value, AwJson.Serialize(file, options));
            return ValueTask.FromResult(Results.Success(resolved.Value));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return ValueTask.FromResult<Result<string>>(ContextErrors.StoreUnavailable.With($"{resolved.Value} can't be written: {e.Message}"));
        }
    }

    public async ValueTask<Result<ReviewFile>> Read(string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.Any(char.IsControl) || !Path.IsPathRooted(path) || RealPath.Of(path) is not { } full)
        {
            return ContextErrors.MapFileInvalid.With("the review file's path isn't an absolute, resolvable path without control characters; nothing was applied (fix: give its full path)");
        }

        if (Worktrees.Containing(full) is { } worktree)
        {
            return ContextErrors.PathInWorktree.With($"{full} is inside the git worktree {worktree} (fix: keep review files outside every repo)");
        }

        byte[] bytes;
        try
        {
            if (new FileInfo(full).Length > MaxBytes)
            {
                return ContextErrors.MapFileInvalid.With($"{full}: over 64 MB; nothing was applied");
            }

            bytes = await File.ReadAllBytesAsync(full, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return ContextErrors.MapFileInvalid.With($"{full} can't be read: {e.Message}");
        }

        var parsed = Parse(bytes);
        return parsed.IsError
            ? ContextErrors.MapFileInvalid.With($"{full}: {parsed.Error.Message}; nothing was applied (fix: correct the file and apply it again)")
            : parsed.Value;
    }

    private Result<ReviewFile> Parse(byte[] bytes)
    {
        var text = Encoding.UTF8.GetString(bytes);
        if (text.TrimStart().StartsWith('{'))
        {
            return AwJson.Deserialize<ReviewFile>(bytes, options);
        }

        return YamlToJson.TryConvert(text, out var json) is { } problem
            ? KernelErrors.Invalid.With(problem)
            : AwJson.Deserialize<ReviewFile>(json, options);
    }
}
