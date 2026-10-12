using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Testing;

/// <summary>The live profile and its approvals in memory; passes <see cref="ApprovalsContract"/>.</summary>
public sealed class InMemoryProfiles : IProfileSource, IApprovals
{
    private LoadedProfile? _approved;

    /// <summary>What the live file holds: a profile, or the error loading it gives.</summary>
    public Result<LoadedProfile> Live { get; set; } = SindriErrors.ProfileMissing.With("no profile in this test");

    /// <inheritdoc/>
    public ValueTask<Result<LoadedProfile>> Load(CancellationToken cancellationToken) => ValueTask.FromResult(Live);

    /// <inheritdoc/>
    public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken) => ValueTask.FromResult<Result<LoadedProfile?>>(_approved);

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Save(LoadedProfile profile, CancellationToken cancellationToken)
    {
        _approved = profile;
        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }
}
