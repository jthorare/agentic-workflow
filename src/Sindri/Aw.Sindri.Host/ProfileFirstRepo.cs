using Aw.Sindri.Core.Graph.Templates;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Host;

/// <summary>The first repo of the live profile, for a data template's agent step (#3509).</summary>
/// <param name="profile">The approved profile.</param>
internal sealed class ProfileFirstRepo(IApprovedProfile profile) : IFirstRepo
{
    public async ValueTask<string?> Name(CancellationToken cancellationToken)
    {
        var latest = await profile.Latest(cancellationToken).ConfigureAwait(false);
        return latest.IsSuccess && latest.Value is { } loaded ? loaded.Profile.Repos?.FirstOrDefault()?.Name : null;
    }
}
