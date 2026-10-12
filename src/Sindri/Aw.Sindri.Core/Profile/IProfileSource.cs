using Aw.Kernel;

namespace Aw.Sindri.Core.Profile;

/// <summary>The live profile files (port): read, validated and typed.</summary>
public interface IProfileSource
{
    /// <summary>Loads the live profile.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The profile, SND-PROFILE-001 when there is none, or SND-PROFILE-002 naming the file, key and fix.</returns>
    public ValueTask<Result<LoadedProfile>> Load(CancellationToken cancellationToken);
}
