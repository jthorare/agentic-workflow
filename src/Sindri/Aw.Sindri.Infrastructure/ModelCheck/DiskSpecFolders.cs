using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.ModelCheck;

namespace Aw.Sindri.Infrastructure.ModelCheck;

/// <summary>
/// <see cref="ISpecFolders"/> on disk. The pattern is a path of the workspace whose segments are literal names or '*' (any one folder), such as 'specs/tla/*/'. Nothing is followed: a link is
/// reported as a link and never read, so a spec folder can't pull in a file from elsewhere, and a linked folder doesn't match.
/// </summary>
internal sealed class DiskSpecFolders : ISpecFolders
{
    private const string Gaps = "gaps";

    public ValueTask<Result<IReadOnlyList<SpecFolder>>> List(string workspace, string pattern, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(pattern);
        var segments = pattern.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (Path.IsPathRooted(pattern) || segments.Length == 0 || segments.Contains("..", StringComparer.Ordinal) || segments.Contains(".", StringComparer.Ordinal))
        {
            return ValueTask.FromResult<Result<IReadOnlyList<SpecFolder>>>(SindriErrors.InvalidInput.With($"'{pattern}' is not a folder pattern inside the workspace (fix: a relative path such as specs/tla/*/)"));
        }

        try
        {
            var matched = Match(workspace, segments, string.Empty, cancellationToken);
            return ValueTask.FromResult<Result<IReadOnlyList<SpecFolder>>>(matched.Order(StringComparer.Ordinal).Select(relative => Read(workspace, relative, cancellationToken)).ToList());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return ValueTask.FromResult<Result<IReadOnlyList<SpecFolder>>>(SindriErrors.InvalidInput.With($"the spec folders of {workspace} cannot be read: {e.Message}"));   // boundary: disk
        }
    }

    // Red stub: the green commit checks the path on disk.
    public ValueTask<bool> Exists(string workspace, string path, CancellationToken cancellationToken) => throw new NotSupportedException("the stale models: path check is not built yet (red)");

    private static List<string> Match(string workspace, string[] segments, string relative, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (segments.Length == 0)
        {
            return Directory.Exists(Path.Combine(workspace, relative)) ? [relative] : [];
        }

        var directory = Path.Combine(workspace, relative);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var names = string.Equals(segments[0], "*", StringComparison.Ordinal)
            ? Directory.EnumerateDirectories(directory).Select(Path.GetFileName).OfType<string>().Where(name => !IsLink(Path.Combine(directory, name)))
            : [segments[0]];
        return [.. names.SelectMany(name => IsLink(Path.Combine(directory, name)) ? [] : Match(workspace, segments[1..], relative.Length == 0 ? name : $"{relative}/{name}", cancellationToken))];
    }

    private static bool IsLink(string path) => new FileInfo(path).LinkTarget is not null || new DirectoryInfo(path).LinkTarget is not null;

    private static SpecFolder Read(string workspace, string relative, CancellationToken cancellationToken)
    {
        var files = new List<SpecFile>();
        ReadEntries(Path.Combine(workspace, relative), string.Empty, files, cancellationToken);
        return new SpecFolder(relative, files);
    }

    private static void ReadEntries(string directory, string prefix, List<SpecFile> files, CancellationToken cancellationToken)
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(directory).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = prefix + Path.GetFileName(path);
            if (IsLink(path))
            {
                files.Add(new SpecFile(name, SpecFileKind.Symlink, string.Empty));
            }
            else if (Directory.Exists(path))
            {
                if (prefix.Length == 0 && string.Equals(Path.GetFileName(path), Gaps, StringComparison.Ordinal))
                {
                    ReadEntries(path, Gaps + "/", files, cancellationToken);
                }
                else
                {
                    files.Add(new SpecFile(name, SpecFileKind.Other, string.Empty));
                }
            }
            else if (File.Exists(path))
            {
                files.Add(new SpecFile(name, SpecFileKind.Regular, File.ReadAllText(path)));
            }
            else
            {
                files.Add(new SpecFile(name, SpecFileKind.Other, string.Empty));
            }
        }
    }
}
