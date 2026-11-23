using Aw.Sindri.Core.Setup;
using Aw.Sindri.Infrastructure.Workflow;

namespace Aw.Sindri.Infrastructure.Setup;

/// <summary>
/// <see cref="IRepoScanTree"/> over the file system (first-run spec §3.3). Folders come from one directory listing each, links left out; the '.git' entry and '.git/worktrees' are read without git; the origin is
/// the one git call, 'git config --file .git/config --get remote.origin.url' (hardened, an argument list), so a checkout whose repository is broken still names its origin. Nothing is written.
/// </summary>
/// <param name="home">The user's home folder, real.</param>
internal sealed class FileRepoScanTree(string home) : IRepoScanTree
{
    public string Home { get; } = home;

    public string? Real(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return RealPath.Of(path) is { } real && Directory.Exists(real) && Readable(real) ? real : null;
    }

    public IReadOnlyList<string> Folders(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        try
        {
            return [.. new DirectoryInfo(folder).EnumerateDirectories().Where(static d => d.LinkTarget is null).Select(static d => d.FullName).Order(StringComparer.Ordinal)];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return [];
        }
    }

    public GitMarker Marker(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        var git = Path.Combine(folder, ".git");
        return Directory.Exists(git) ? GitMarker.Folder : File.Exists(git) ? GitMarker.File : GitMarker.None;
    }

    public int Worktrees(string checkout)
    {
        ArgumentNullException.ThrowIfNull(checkout);
        try
        {
            var worktrees = new DirectoryInfo(Path.Combine(checkout, ".git", "worktrees"));
            return worktrees.Exists ? worktrees.EnumerateDirectories().Count() : 0;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return 0;
        }
    }

    public async ValueTask<string?> Origin(string checkout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(checkout);
        var config = Path.Combine(checkout, ".git", "config");
        if (!File.Exists(config))
        {
            return null;
        }

        var read = await GitProcess.Run(checkout, ["config", "--file", config, "--get", "remote.origin.url"], cancellationToken, hardened: true).ConfigureAwait(false);
        return read is { Exit: 0, Output.Length: > 0 } ? read.Output : null;
    }

    private static bool Readable(string directory)
    {
        try
        {
            using var entries = Directory.EnumerateFileSystemEntries(directory).GetEnumerator();
            _ = entries.MoveNext();
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
