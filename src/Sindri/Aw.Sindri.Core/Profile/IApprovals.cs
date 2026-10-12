using Aw.Kernel;

namespace Aw.Sindri.Core.Profile;

/// <summary>Approved profile snapshots (port; design spec §8.7): runtime reads the latest approval, never the live files.</summary>
public interface IApprovals
{
    /// <summary>The most recently approved profile, or null when none was approved.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The snapshot.</returns>
    public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken);

    /// <summary>Records <paramref name="profile"/> as the latest approval.</summary>
    /// <param name="profile">The profile, already checked against the hash the human typed.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success.</returns>
    public ValueTask<Result<Unit>> Save(LoadedProfile profile, CancellationToken cancellationToken);
}
