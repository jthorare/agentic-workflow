using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>The <see cref="ILinearIssueReads"/> over the Linear source factory: one reader (and so one key resolution) per call.</summary>
/// <param name="linear">The factory of Linear's parts.</param>
internal sealed class LinearIssueReads(LinearSources linear) : ILinearIssueReads
{
    /// <inheritdoc />
    public ValueTask<Result<RecordItem?>> Read(LinearSettings settings, IReadOnlyList<string> githubRepos, ExternalId issueId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(githubRepos);
        return linear.CreateIssueReader(settings, githubRepos).Read(issueId, cancellationToken);
    }
}
