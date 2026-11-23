using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>Where 'rule_catalog' reads the repo's rule records (issue 3787). Read-only: the Infrastructure adapter reads each 'rules/&lt;kind&gt;/&lt;language&gt;/*.yaml' record.</summary>
public interface IRuleCatalogPort
{
    /// <summary>Every readable rule record, sorted by id; a record that can't be parsed or has no id or text is counted, not listed.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The rules and the count of records skipped; none when the rules folder is missing.</returns>
    public ValueTask<Result<RuleCatalogListing>> List(CancellationToken cancellationToken);
}
