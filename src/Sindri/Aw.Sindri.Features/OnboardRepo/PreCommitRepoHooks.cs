using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.OnboardRepo;

/// <summary>The hook slot's real adapter (cutover spec §6.2 item 2): it installs the v3 pre-commit hook into the repo through the same installer as 'scrub --install-pre-commit', with the stable ~/.local/bin/sindri baked in. A foreign hook is left alone and the sentence says so.</summary>
/// <param name="installer">Writes the hook.</param>
/// <param name="git">Names the stable binary.</param>
internal sealed class PreCommitRepoHooks(IPreCommitInstaller installer, IPreCommitGit git) : IRepoHooks
{
    public async ValueTask<string> Install(RepoEntry repo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        var done = await installer.Install(repo.Path, git.StableBin, cancellationToken).ConfigureAwait(false);
        return done.IsError
            ? $"No secret-check hook installed: {done.Error.Code} {done.Error.Message}"
            : $"Installed the secret-scan pre-commit hook at {done.Value}";
    }
}
