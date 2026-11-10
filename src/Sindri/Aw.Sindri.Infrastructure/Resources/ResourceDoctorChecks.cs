using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Resources;

namespace Aw.Sindri.Infrastructure.Resources;

/// <summary>The doctor's 'resources' row: reads the files for what exists and the bookkeeping rows for each disk's limit.</summary>
/// <param name="files">The provisioned resources on disk.</param>
/// <param name="book">Reads the 'resource' rows.</param>
internal sealed class ResourceDoctorChecks(IResourceFiles files, ResourceProvisioner book) : IDoctorChecks
{
    public async ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
    {
        var listed = files.List(owner: null);
        if (listed.IsError)
        {
            return [new DoctorCheck("resources", CheckState.Warn, listed.Error.Message, "check the resources folder under the Sindri home")];
        }

        var rows = await book.BookedAll(cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return [new DoctorCheck("resources", CheckState.Warn, rows.Error.Message, "check that ledger.db under the Sindri home is readable, then run sindri doctor again")];
        }

        var limits = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var row in rows.Value)
        {
            if (row.SizeLimit is { } limit && string.Equals(row.Kind, "disk", StringComparison.Ordinal))
            {
                limits[$"{row.Owner}/{row.Name}"] = limit;
            }
        }

        return [ResourceDoctorRule.Check(listed.Value, limits)];
    }
}
