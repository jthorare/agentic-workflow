using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.OnboardRepo;

/// <summary>The hook slot until the installer ships: it installs nothing and says so, so the repo is never shown as covered by a secret check it does not have.</summary>
internal sealed class NoSecretCheckHook : IRepoHooks
{
    /// <summary>The detail the repo step shows (setup-once spec §5.1).</summary>
    internal const string Detail = "No secret-check hook yet: add it when the installer ships";

    public ValueTask<string> Install(RepoEntry repo, CancellationToken cancellationToken) => ValueTask.FromResult(Detail);
}
