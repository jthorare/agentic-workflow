using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary><see cref="IForEachLimitSource"/> over the live profile's 'workflow.forEach' block; a key it leaves out, or a profile that can't be read, gives the default.</summary>
/// <param name="profile">The live profile.</param>
internal sealed class ProfileForEachLimits(IApprovedProfile profile) : IForEachLimitSource
{
    public async ValueTask<ForEachLimits> Current(CancellationToken cancellationToken)
    {
        var latest = await profile.Latest(cancellationToken).ConfigureAwait(false);
        var set = latest.IsError ? null : latest.Value?.Profile.Workflow?.ForEach;
        return new ForEachLimits(set?.MaxItems ?? ForEachLimits.DefaultMaxItems, set?.BatchSize ?? ForEachLimits.DefaultBatchSize);
    }
}
