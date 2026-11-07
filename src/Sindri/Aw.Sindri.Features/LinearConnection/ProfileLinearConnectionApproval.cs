using Aw.Kernel;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.LinearConnection;

/// <summary>
/// The one-click approval of a connection change, as the login save does it: when every key that differs from the latest approval is a `context.sources.linear` key, the save approves the profile, with the same
/// 'profile.approved' event and snapshot an approval records. When anything else waits, or nothing was ever approved, nothing is approved.
/// </summary>
/// <param name="live">The live profile.</param>
/// <param name="approval">Records the approval.</param>
internal sealed class ProfileLinearConnectionApproval(IProfileSource live, ProfileChangeApproval approval) : ILinearConnectionApproval
{
    private const string Linear = "context.sources.linear";

    public async ValueTask<Result<bool>> Settle(CancellationToken cancellationToken)
    {
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        return loaded.IsError
            ? loaded.Error
            : await approval.Settle(loaded.Value, previous => ProfileDiff.Between(previous.Profile, loaded.Value.Profile).All(static k => k == Linear || k.StartsWith(Linear + ".", StringComparison.Ordinal)), cancellationToken).ConfigureAwait(false);
    }
}
