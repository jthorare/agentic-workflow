using Aw.Kernel;

namespace Aw.Sindri.Core.Profile;

/// <summary>
/// The profile as a read surface sees it (#3435): an invalid live profile (SND-PROFILE-002) reads like no profile, so a page,
/// a list or a launch picker shows its no-profile state instead of failing. Status and doctor still name the invalid file.
/// </summary>
public static class ApprovedProfileReads
{
    /// <summary>The live profile, or null when there is none or it is invalid; every other error stays an error.</summary>
    /// <param name="approvals">The profile source.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The profile, or null.</returns>
    public static async ValueTask<Result<LoadedProfile?>> ForRead(this IApprovedProfile approvals, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(approvals);
        var latest = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        return latest.IsError && string.Equals(latest.Error.Code, SindriErrors.ProfileInvalid.Code, StringComparison.Ordinal)
            ? Results.Success<LoadedProfile?>(value: null)
            : latest;
    }
}
