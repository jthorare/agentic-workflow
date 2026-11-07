using Aw.Context.Core.Audience;
using Aw.Context.Core.Records;
using Aw.Kernel;

namespace Aw.Context.Core.Facade;

/// <summary>The stored pull requests (PR review page spec §6.1): the Reviews list reads them from the store, never from GitHub.</summary>
public interface IContextPulls
{
    /// <summary>Every open or draft pull request the GitHub source stored, that <paramref name="audience"/> may read.</summary>
    /// <param name="audience">The caller's audience.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The records in key order, or the store's error.</returns>
    public ValueTask<Result<IReadOnlyList<StoredRecord>>> OpenPulls(AudienceScope audience, CancellationToken cancellationToken);
}
