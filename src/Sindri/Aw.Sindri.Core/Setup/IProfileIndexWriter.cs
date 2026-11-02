using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Setup;

/// <summary>
/// Sets or removes entries of the live profile's index by id through the versioned write: concepts in the top-level 'index.concepts' (concept pins spec §3.6, "The write") and pages in the top-level 'index.pages' or a
/// repo's own 'repos.&lt;name&gt;.index.pages' (pages spec §3.1): the file is read, the new text is built and checked, and it replaces the
/// file only if the file is still what was read; on a conflict it re-reads and tries once more. It is a text edit, so every other key and comment is kept; a comment inside an entry it replaces or removes goes with it.
/// Every entry it writes has an 'id'.
/// </summary>
public interface IProfileIndexWriter
{
    /// <summary>Applies <paramref name="edits"/> in one write.</summary>
    /// <param name="edits">Each one sets an entry (inserted after the last entry when its id is new, replaced in place when it is there) or removes one.</param>
    /// <param name="ifHash">The live profile hash the caller read; when given and the file's hash differs, nothing is written. Null reads the live file and writes once.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The new file's hash; SND-PROFILE-003 when <paramref name="ifHash"/> isn't the file's hash; SND-PROFILE-002 when the file has a layout Sindri won't edit, changed twice while writing, or can't be written; SND-PROFILE-001 when it can't be read or there is none.</returns>
    public ValueTask<Result<ProfileHash>> Write(IReadOnlyList<IndexEdit> edits, ProfileHash? ifHash, CancellationToken cancellationToken);
}
