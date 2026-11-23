using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>
/// A directory where the profile file belongs (#3833). File.Exists is false for it, so without this check it reads as "no profile" and setup offers a
/// create that can't succeed. It is SND-PROFILE-004: something is at the path and Sindri can't read it as a profile.
/// </summary>
internal static class ProfileDirectoryInTheWay
{
    /// <summary>SND-PROFILE-004 naming the directory when <paramref name="path"/> is one; null otherwise.</summary>
    /// <param name="path">The profile file's path.</param>
    /// <returns>The error, or null when no directory is at the path.</returns>
    internal static Error? At(string path) =>
        Directory.Exists(path)
            ? SindriErrors.ProfileUnreadable.With($"{path} is a directory, not a file (fix: move that folder out of the way; the profile is a single file at that path)")
            : null;
}
