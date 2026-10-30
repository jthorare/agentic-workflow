using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>Writes a Step's fact files into its run directory.</summary>
internal static class StepFactWriter
{
    /// <summary>The folder in the run directory the agent writes its outputs to; the agent brief names the same folder.</summary>
    internal const string OutputFolder = ".sindri-out";

    private const string Extension = ".json";
    private const string Marker = "# sindri step fact files (written by the launcher)";

    /// <summary>
    /// Writes each file under <paramref name="runDirectory"/>, replacing our own file an earlier attempt left. When the directory is in a git worktree, a name git
    /// tracks refuses the write (the tracked file is never touched), and the names go into the worktree's local 'info/exclude' so the Step's 'git add' skips them.
    /// </summary>
    /// <param name="runDirectory">The run directory.</param>
    /// <param name="files">File name to content.</param>
    /// <param name="cancellationToken">Cancels the git queries.</param>
    /// <returns>An error when a name would leave the directory or is a tracked file (nothing is written then); null when every file is written.</returns>
    internal static async ValueTask<Error?> Write(string runDirectory, IReadOnlyDictionary<string, string> files, CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(runDirectory);
        foreach (var name in files.Keys)
        {
            if (!string.Equals(Path.GetFileName(name), name, StringComparison.Ordinal) || name.AsSpan().Trim('.').IsEmpty)
            {
                return SindriErrors.RequestInvalid.With($"the fact file '{name}' is not one file in the run directory, so no fact files were written");
            }
        }

        _ = Directory.CreateDirectory(root);
        var exclude = await ExcludeFile(root, cancellationToken).ConfigureAwait(false);   // null outside a git checkout: nothing to guard
        if (exclude is not null && await Tracked(root, files.Keys, cancellationToken).ConfigureAwait(false) is { } tracked)
        {
            return tracked;
        }

        try
        {
            if (exclude is not null)
            {
                await Exclude(exclude, files.Keys, cancellationToken).ConfigureAwait(false);
            }

            foreach (var (name, content) in files)
            {
                await File.WriteAllTextAsync(Path.Combine(root, name), content, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return KernelErrors.Unavailable.With($"the Step's fact files could not be written: {e.Message}");
        }

        return null;
    }

    /// <summary>
    /// Makes the Step's output folder in its run directory and, in a git worktree, adds it to the local 'info/exclude' (once) before the agent runs, so the files the agent writes for
    /// 'workflow complete --output' are never untracked work: the Step's 'git add', 'status', 'ls-files --exclude-standard' and the worktree removal check all skip them.
    /// </summary>
    /// <param name="runDirectory">The run directory.</param>
    /// <param name="cancellationToken">Cancels the git queries.</param>
    /// <returns>An error when the folder or the exclude line could not be written; null when both are in place.</returns>
    internal static async ValueTask<Error?> Outputs(string runDirectory, CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(runDirectory);
        try
        {
            _ = Directory.CreateDirectory(Path.Combine(root, OutputFolder));
            if (await ExcludeFile(root, cancellationToken).ConfigureAwait(false) is { } exclude)
            {
                await Exclude(exclude, [OutputFolder + "/"], cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return KernelErrors.Unavailable.With($"the Step's output folder could not be made: {e.Message}");
        }

        return null;
    }

    /// <summary>In a git worktree, adds <paramref name="names"/> (paths relative to the worktree) to the local 'info/exclude' (once, under the same marker) so the Step's 'git add' skips files the launcher puts in the checkout, such as a cursor-config policy's .cursor/cli.json.</summary>
    /// <param name="workDir">The Step's worktree.</param>
    /// <param name="names">The paths to exclude.</param>
    /// <param name="cancellationToken">Cancels the git query.</param>
    /// <returns>An error when the exclude file could not be written; null when the lines are in place or the directory is no checkout.</returns>
    internal static async ValueTask<Error?> Excluded(string workDir, IEnumerable<string> names, CancellationToken cancellationToken)
    {
        try
        {
            if (await ExcludeFile(Path.GetFullPath(workDir), cancellationToken).ConfigureAwait(false) is { } exclude)
            {
                await Exclude(exclude, names, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return KernelErrors.Unavailable.With($"the git exclude for the Step's files could not be written: {e.Message}");
        }

        return null;
    }

    // The refusal for the first name git tracks in the directory; null when none is.
    private static async ValueTask<Error?> Tracked(string root, IEnumerable<string> names, CancellationToken cancellationToken)
    {
        foreach (var name in names.Order(StringComparer.Ordinal))
        {
            if (await ViewerProcess.Output("git", ["-C", root, "ls-files", "--", name], cancellationToken).ConfigureAwait(false) is { Length: > 0 })
            {
                var fact = name.EndsWith(Extension, StringComparison.Ordinal) ? name[..^Extension.Length] : name;
                return SindriErrors.LaunchFactFileTracked.With($"the fact 'fact:{fact}' would be written to {Path.Combine(root, name)}, which git tracks (fix: rename or untrack that file; a Step's fact files are never written over a tracked file)");
            }
        }

        return null;
    }

    // The worktree's own exclude file ('git rev-parse --git-path info/exclude' is the shared one of a linked worktree's repo, which is what git reads); null outside a checkout.
    private static async ValueTask<string?> ExcludeFile(string root, CancellationToken cancellationToken) =>
        await ViewerProcess.Output("git", ["-C", root, "rev-parse", "--git-path", "info/exclude"], cancellationToken).ConfigureAwait(false) is { Length: > 0 } path
            ? Path.GetFullPath(path, root)
            : null;

    private static async Task Exclude(string exclude, IEnumerable<string> names, CancellationToken cancellationToken)
    {
        var present = File.Exists(exclude) ? await File.ReadAllLinesAsync(exclude, cancellationToken).ConfigureAwait(false) : [];
        var missing = names.Where(n => !present.Contains("/" + n, StringComparer.Ordinal)).Order(StringComparer.Ordinal).Select(static n => "/" + n).ToArray();
        if (missing.Length == 0)
        {
            return;
        }

        _ = Directory.CreateDirectory(Path.GetDirectoryName(exclude)!);   // why: a path always has a directory
        var lead = File.Exists(exclude) && new FileInfo(exclude).Length > 0 && !(await File.ReadAllTextAsync(exclude, cancellationToken).ConfigureAwait(false)).EndsWith('\n') ? "\n" : string.Empty;
        var marker = present.Contains(Marker, StringComparer.Ordinal) ? string.Empty : Marker + "\n";   // a person can see what added the lines, and remove them
        await File.AppendAllTextAsync(exclude, lead + marker + string.Join('\n', missing) + "\n", cancellationToken).ConfigureAwait(false);
    }
}
