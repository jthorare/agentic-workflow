using Aw.Kernel;
using Aw.Sindri.Core.Checks;

namespace Aw.Sindri.Core.Setup;

/// <summary>The machine side of the pre-commit hook (cutover spec §6.2): where git says a repo's hook lives, what its staged diff adds, the hook file itself, and the binary paths. The adapter is the only place that starts git or touches a hook file.</summary>
public interface IPreCommitGit
{
    /// <summary>The stable wrapper, ~/.local/bin/sindri, which every installed hook bakes in (never a stamped build directory, which pruning deletes).</summary>
    public string StableBin { get; }

    /// <summary>The running binary's own path; the default 'scrub --install-pre-commit' bakes in.</summary>
    public string RunningBin { get; }

    /// <summary>Reads a hook file.</summary>
    /// <param name="path">The hook's path.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The text, or null when there is no file.</returns>
    public ValueTask<string?> ReadHook(string path, CancellationToken cancellationToken);

    /// <summary>Writes a hook file, creating its directory, and marks it executable.</summary>
    /// <param name="path">The hook's path.</param>
    /// <param name="text">The script.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the file is written.</returns>
    public ValueTask WriteHook(string path, string text, CancellationToken cancellationToken);

    /// <summary>Resolves the hook path with 'git rev-parse --git-path hooks/pre-commit'.</summary>
    /// <param name="repo">The repo's folder; null is the current directory.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The location, or SND-SCRUB-004 when the folder is not a git repo.</returns>
    public ValueTask<Result<HookLocation>> HookPath(string? repo, CancellationToken cancellationToken);

    /// <summary>Reads the lines the staged diff adds ('git diff --cached --unified=0').</summary>
    /// <param name="repo">The repo's folder; null is the current directory.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The added lines, SND-SCRUB-004 outside a repo, or SND-SCRUB-005 when the diff failed.</returns>
    public ValueTask<Result<IReadOnlyList<AddedLine>>> StagedAddedLines(string? repo, CancellationToken cancellationToken);
}
