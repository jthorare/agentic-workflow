using Aw.Kernel;

namespace Aw.Sindri.Core.Profile;

/// <summary>Reads profile YAML that is not on disk yet with the reader <see cref="IProfileSource"/> uses, so an edit can be checked before it is written.</summary>
public interface IProfileTextParser
{
    /// <summary>Parses and checks <paramref name="yaml"/>.</summary>
    /// <param name="yaml">The profile text.</param>
    /// <param name="source">The file it is meant for, for messages.</param>
    /// <returns>The checked profile; SND-PROFILE-002 naming the key path and the fix.</returns>
    public Result<LoadedProfile> Parse(string yaml, string source);

    /// <summary>Checks and hashes a profile that is already typed, the way <see cref="Parse"/> does after reading the text.</summary>
    /// <param name="profile">The typed profile.</param>
    /// <param name="source">The file it is meant for, for messages.</param>
    /// <returns>The checked profile with its hash; SND-PROFILE-002 naming the key path and the fix.</returns>
    public Result<LoadedProfile> Recheck(SindriProfile profile, string source);
}
