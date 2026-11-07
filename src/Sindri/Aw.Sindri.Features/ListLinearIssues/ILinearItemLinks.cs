using Aw.Kernel;

namespace Aw.Sindri.Features.ListLinearIssues;

/// <summary>The items linked to Linear issues and their pull requests.</summary>
internal interface ILinearItemLinks
{
    /// <summary>Finds the item and the pull request of each record.</summary>
    /// <param name="records">Issue record keys, 'linear:issue/&lt;uuid&gt;'.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The links by record key; a record with none is left out.</returns>
    public ValueTask<Result<IReadOnlyDictionary<string, LinearIssueLinks>>> For(IReadOnlyList<string> records, CancellationToken cancellationToken);
}
