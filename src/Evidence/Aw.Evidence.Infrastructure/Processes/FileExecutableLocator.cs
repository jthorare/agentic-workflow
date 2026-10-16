using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Infrastructure.Processes;

/// <summary>Resolves paths on the real file system: every symlink followed, and only an executable regular file is an executable.</summary>
internal sealed class FileExecutableLocator : IExecutableLocator
{
    private const int MaxLinks = 40;
    private const UnixFileMode AnyExecute = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    /// <inheritdoc/>
    public string? RealExecutable(string path) =>
        RealPath(path) is { } real && File.Exists(real) && IsExecutable(real) ? real : null;

    /// <inheritdoc/>
    public string? RealFile(string path) => RealPath(path) is { } real && File.Exists(real) ? real : null;

    /// <inheritdoc/>
    public string? RealDirectory(string path) => RealPath(path) is { } real && Directory.Exists(real) ? real : null;

    private static bool IsExecutable(string file) => OperatingSystem.IsWindows() || (File.GetUnixFileMode(file) & AnyExecute) != UnixFileMode.None;

    // The path with every symlink in it followed, directories on the way included (File.ResolveLinkTarget only follows the last one).
    private static string? RealPath(string path)
    {
        if (!path.StartsWith('/'))
        {
            return null;
        }

        var pending = new Stack<string>(path.Split('/', StringSplitOptions.RemoveEmptyEntries).Reverse());
        var resolved = string.Empty;
        var links = 0;
        while (pending.TryPop(out var segment))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                resolved = resolved[..Math.Max(0, resolved.LastIndexOf('/'))];
                continue;
            }

            var next = resolved + "/" + segment;
            if (new FileInfo(next).LinkTarget is not { } target)
            {
                resolved = next;
                continue;
            }

            if (++links > MaxLinks)
            {
                return null;
            }

            resolved = target.StartsWith('/') ? string.Empty : resolved;
            PushSegments(pending, target);
        }

        return resolved.Length == 0 ? "/" : resolved;
    }

    private static void PushSegments(Stack<string> pending, string target)
    {
        foreach (var part in target.Split('/', StringSplitOptions.RemoveEmptyEntries).Reverse())
        {
            pending.Push(part);
        }
    }
}
