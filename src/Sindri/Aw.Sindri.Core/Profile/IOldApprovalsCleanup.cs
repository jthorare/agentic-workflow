using Aw.Kernel;

namespace Aw.Sindri.Core.Profile;

/// <summary>Once, at start: removes the old &lt;home&gt;/approved folder and notes it in the ledger (#2258 PR 1).</summary>
public interface IOldApprovalsCleanup
{
    /// <summary>Removes the folder when it is there, and says so in the ledger.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or why the note or the removal failed.</returns>
    public ValueTask<Result<Unit>> Run(CancellationToken cancellationToken);
}
