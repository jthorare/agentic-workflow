using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Core.Audience;

/// <summary>Who may see which records, resolved from the approved profile (spec §4.4): never from raw link state alone, and re-read when a profile is re-approved.</summary>
public interface IAudiencePolicy
{
    /// <summary>The audiences a session scoped to a repo may read: its own repo's records, and the teams and channels the approved profile maps to that repo.</summary>
    /// <param name="repo">The session's repo.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The scope; a repo the profile doesn't name gets <see cref="AudienceScope.None"/>.</returns>
    public ValueTask<Result<AudienceScope>> For(ContextRepoName repo, CancellationToken cancellationToken);
}
