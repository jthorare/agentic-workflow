using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests;

/// <summary>Approvals whose snapshot can't be read (SND-PROFILE-006); saving still works, so an approval can repair it.</summary>
internal sealed class ReadFailingApprovals(IApprovals inner) : IApprovals
{
    public static readonly Error Failure = SindriErrors.ApprovalsUnavailable.With("approved/latest can't be read (test)");

    public bool Failing { get; set; } = true;

    public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken) =>
        Failing ? ValueTask.FromResult<Result<LoadedProfile?>>(Failure) : inner.Latest(cancellationToken);

    public ValueTask<Result<Unit>> Save(LoadedProfile profile, CancellationToken cancellationToken) => inner.Save(profile, cancellationToken);
}
