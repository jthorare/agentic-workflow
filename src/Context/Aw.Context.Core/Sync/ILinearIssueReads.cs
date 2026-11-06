using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Core.Sync;

/// <summary>Re-reads one Linear issue the way the record feed reads a page (Linear hub spec 2026-11-04 D4), for the write-through: the same query, the same mapper, so the item is what the next tick would hand over for the same version.</summary>
public interface ILinearIssueReads
{
    /// <summary>Reads one issue with the read key.</summary>
    /// <param name="settings">The checked 'context.sources.linear' settings.</param>
    /// <param name="githubRepos">'context.sources.github.repos' ('owner/name'): an issue's pull request attachment links to its PR record only for these.</param>
    /// <param name="issueId">Linear's id of the issue.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The item with its side rows; null when Linear has no such issue or it is not in one of the profile's teams; the source's error when Linear can't be asked.</returns>
    public ValueTask<Result<RecordItem?>> Read(LinearSettings settings, IReadOnlyList<string> githubRepos, ExternalId issueId, CancellationToken cancellationToken);
}
