using Aw.Kernel;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Setup;

/// <summary>
/// Appends a repo to the live profile file (setup-once spec §5, §5.1) through a versioned write: the file is read, the new text is checked, and the write is made only if the file is still what was read; on a
/// conflict it re-reads and tries once more. It keeps every other key and comment. The first repo creates the file.
/// </summary>
public interface IProfileRepoWriter
{
    /// <summary>Adds <paramref name="repo"/> to 'repos:'.</summary>
    /// <param name="repo">The entry (name and real path).</param>
    /// <param name="defaultEffects">The effect kinds a new file allows (<see cref="IShippedEffects"/>); used only when there is no file.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Created, Appended or AlreadyThere; SND-SETUP-002 when the name or path belongs to another entry; SND-PROFILE-002 when the file has a layout Sindri won't edit or changed twice while writing, or can't be written (with the OS reason); SND-PROFILE-001 when it can't be read.</returns>
    public ValueTask<Result<RepoAppended>> Append(RepoEntry repo, IReadOnlyCollection<string> defaultEffects, CancellationToken cancellationToken);
}
