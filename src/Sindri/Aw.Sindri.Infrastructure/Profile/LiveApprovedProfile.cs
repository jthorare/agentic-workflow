using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>
/// The profile runtime reads once approval is gone (#2258 PR 1): the live file, validated on each read (<see cref="IProfileSource.Load"/> checks it). A missing file is "no profile" (null), as an
/// absent approval was; an invalid one is its SND-PROFILE error, so nothing runs on a profile that wouldn't pass validation.
/// </summary>
/// <param name="source">The live profile.</param>
internal sealed class LiveApprovedProfile(IProfileSource source) : IApprovedProfile
{
    internal IProfileSource Source { get; } = source;

    public async ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken)
    {
        var loaded = await Source.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsSuccess)
        {
            return Results.Success<LoadedProfile?>(loaded.Value);
        }

        return loaded.Error.Code == SindriErrors.ProfileMissing.Code ? Results.Success<LoadedProfile?>(value: null) : loaded.Error;
    }
}
