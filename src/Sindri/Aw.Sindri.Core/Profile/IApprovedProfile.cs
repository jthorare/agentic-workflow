using Aw.Kernel;

namespace Aw.Sindri.Core.Profile;

/// <summary>
/// The profile runtime reads (#2258 PR 1: edits apply on save, so this is the live profile, validated). The name stays for one
/// release so the injection sites don't all change at once.
/// </summary>
public interface IApprovedProfile
{
    /// <summary>The live profile, or null when there is no profile file.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The profile.</returns>
    public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken);
}
