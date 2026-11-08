using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Testing;

/// <summary>The live profile in memory (#2258 PR 1: edits apply on save, so the profile runtime reads is the live one); also the <see cref="IApprovedProfile"/> the services take.</summary>
public sealed class InMemoryProfiles : IProfileSource, IApprovedProfile
{
    /// <summary>What the live file holds: a profile, or the error loading it gives.</summary>
    public Result<LoadedProfile> Live { get; set; } = SindriErrors.ProfileMissing.With("no profile in this test");

    /// <inheritdoc/>
    public ValueTask<Result<LoadedProfile>> Load(CancellationToken cancellationToken) => ValueTask.FromResult(Live);

    /// <inheritdoc/>
    public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Live.IsSuccess ? Results.Success<LoadedProfile?>(Live.Value) : Live.Error.Code == SindriErrors.ProfileMissing.Code ? Results.Success<LoadedProfile?>(value: null) : Live.Error);

    /// <summary>Makes <paramref name="profile"/> the live profile, as a write to the file does.</summary>
    /// <param name="profile">The profile.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success.</returns>
    public ValueTask<Result<Unit>> Save(LoadedProfile profile, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Live = profile;
        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }
}
