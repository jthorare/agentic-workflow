using Aw.Kernel;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.GetStatus;

/// <summary>The two profile reads the status makes: the approved profile and the live file; and what it says of the Jev label pick, which needs to know whether Jev has a key.</summary>
/// <param name="approvals">The approved profile.</param>
/// <param name="source">The live file.</param>
/// <param name="jevKey">Says whether Jev has a key; null counts it present (a host that registers no probe).</param>
internal sealed class ProfileReads(IApprovedProfile approvals, IProfileSource source, IJevKeyProbe? jevKey = null)
{
    /// <summary>The label-pick verdict of the approved profile: 'off', 'on' or 'needs-key'.</summary>
    /// <param name="approved">The approved profile, or null.</param>
    /// <returns>The verdict.</returns>
    internal string LabelPick(LoadedProfile? approved) => LabelPickSetting.Of(approved, jevKey);

    internal ValueTask<Result<LoadedProfile?>> Approved(CancellationToken cancellationToken) => approvals.Latest(cancellationToken);

    internal ValueTask<Result<LoadedProfile>> Live(CancellationToken cancellationToken) => source.Load(cancellationToken);
}
