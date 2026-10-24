using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.OnboardRepo;

#pragma warning disable MA0182 // until cutover unit 4.1 registers it in place of NoSecretCheckHook
/// <summary>Red until cutover unit 4.1: the hook slot's real adapter, which installs the v3 pre-commit hook into the repo through the same installer as 'scrub --install-pre-commit', with the stable ~/.local/bin/sindri baked in. Not registered until 4.1 swaps it for <see cref="NoSecretCheckHook"/>.</summary>
internal sealed class PreCommitRepoHooks : IRepoHooks
{
    public ValueTask<string> Install(RepoEntry repo, CancellationToken cancellationToken) =>
        throw new NotSupportedException("the pre-commit installer is built in cutover unit 4.1");
}
#pragma warning restore MA0182
