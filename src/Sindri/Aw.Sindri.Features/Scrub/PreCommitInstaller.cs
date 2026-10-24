using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.Scrub;

/// <summary>Writes the v3 pre-commit hook where git says, replacing a TS hook and refusing anyone else's (SND-SCRUB-003). Shared by 'scrub --install-pre-commit' and the onboarding hook slot.</summary>
/// <param name="git">Resolves the hook path.</param>
internal sealed class PreCommitInstaller(IPreCommitGit git) : IPreCommitInstaller
{
    /// <inheritdoc/>
    public async ValueTask<Result<string>> Install(string? repo, string bin, CancellationToken cancellationToken)
    {
        var where = await git.HookPath(repo, cancellationToken).ConfigureAwait(false);
        if (where.IsError)
        {
            return where.Error;
        }

        var path = where.Value.Path;
        if (!where.Value.InGitDir)
        {
            return SindriErrors.ScrubForeignHook.With($"core.hooksPath points at {Path.GetDirectoryName(path)}, which Sindri did not create; add 'sindri scrub --staged' to the hooks there by hand");
        }

        var existing = await git.ReadHook(path, cancellationToken).ConfigureAwait(false);
        if (PreCommitHook.Classify(existing) == HookKind.Foreign)
        {
            return SindriErrors.ScrubForeignHook.With($"{path} is not Sindri's; add 'sindri scrub --staged || exit 1' to it by hand");
        }

        await git.WriteHook(path, PreCommitHook.Body(bin), cancellationToken).ConfigureAwait(false);
        return path;
    }

    /// <inheritdoc/>
    public async ValueTask<HookKind> Kind(string repo, CancellationToken cancellationToken)
    {
        var where = await git.HookPath(repo, cancellationToken).ConfigureAwait(false);
        return where.IsError ? HookKind.None : PreCommitHook.Classify(await git.ReadHook(where.Value.Path, cancellationToken).ConfigureAwait(false));
    }
}
