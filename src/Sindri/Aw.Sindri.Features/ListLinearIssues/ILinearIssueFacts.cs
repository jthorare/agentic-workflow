using Aw.Kernel;

namespace Aw.Sindri.Features.ListLinearIssues;

/// <summary>What the owned index holds of Linear's issues, in Sindri's own records: Context's IContextIssues behind an adapter. Reads stored data only; nothing calls Linear.</summary>
internal interface ILinearIssueFacts
{
    /// <summary>Reads whether a Linear source is set up, who the viewer is and how the issue feed stands.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The facts, or the error Context gave.</returns>
    public ValueTask<Result<LinearSourceFacts>> Source(CancellationToken cancellationToken);

    /// <summary>Reads one page of stored issues for the current caller (a session sees only the teams mapped to its repo).</summary>
    /// <param name="filter">Which issues.</param>
    /// <param name="after">Where to continue, or null for the first page.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The page, or the error Context gave.</returns>
    public ValueTask<Result<LinearIssueFactsPage>> Issues(LinearIssueFactsFilter filter, LinearIssuePosition? after, CancellationToken cancellationToken);
}
