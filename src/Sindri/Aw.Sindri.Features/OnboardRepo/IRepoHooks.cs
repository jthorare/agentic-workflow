using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.OnboardRepo;

/// <summary>The hook slot of onboarding (setup-once spec §5.1 "After approval"): the pre-commit scrub hook of a repo, once the C# build has an installer to put it there.</summary>
internal interface IRepoHooks
{
    /// <summary>Installs the repo's hooks.</summary>
    /// <param name="repo">The approved profile's entry.</param>
    /// <param name="cancellationToken">Cancels the install.</param>
    /// <returns>One sentence saying what was done, shown in the job's result and the doctor line.</returns>
    public ValueTask<string> Install(RepoEntry repo, CancellationToken cancellationToken);
}
