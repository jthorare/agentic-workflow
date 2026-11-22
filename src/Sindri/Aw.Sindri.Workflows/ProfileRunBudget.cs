using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary><see cref="IRunBudgetSource"/> over the live profile's 'workflow.budget.tokens' (#3805); a profile that leaves it out, or can't be read, sets no cap.</summary>
/// <param name="profile">The live profile.</param>
internal sealed class ProfileRunBudget(IApprovedProfile profile) : IRunBudgetSource
{
    public async ValueTask<long?> DefaultTokens(CancellationToken cancellationToken)
    {
        var latest = await profile.Latest(cancellationToken).ConfigureAwait(false);
        return latest.IsError ? null : latest.Value?.Profile.Workflow?.Budget?.Tokens;
    }
}
