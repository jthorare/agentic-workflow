using Aw.Kernel;

namespace Aw.Sindri.Core.Setup;

/// <summary>The pre-commit hook of one repo (cutover spec §6.2): write the v3 hook, or say what is there. Implemented once, by the scrub slice; onboarding and the doctor reach it through this port because slices are isolated.</summary>
public interface IPreCommitInstaller
{
    /// <summary>Writes the v3 hook where git says, replacing a TS hook and refusing anyone else's.</summary>
    /// <param name="repo">The repo's folder; null is the current directory.</param>
    /// <param name="bin">The path baked into the hook.</param>
    /// <param name="cancellationToken">Cancels the install.</param>
    /// <returns>The hook file written, SND-SCRUB-003 for a foreign hook or a core.hooksPath Sindri did not create, or SND-SCRUB-004 outside a repo.</returns>
    public ValueTask<Result<string>> Install(string? repo, string bin, CancellationToken cancellationToken);

    /// <summary>Classifies the repo's pre-commit file.</summary>
    /// <param name="repo">The repo's folder.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The kind; a folder that is not a git repo reads as <see cref="HookKind.None"/>.</returns>
    public ValueTask<HookKind> Kind(string repo, CancellationToken cancellationToken);
}
