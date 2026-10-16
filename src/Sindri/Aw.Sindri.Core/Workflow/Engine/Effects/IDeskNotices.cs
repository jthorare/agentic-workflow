using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Engine.Effects;

/// <summary>
/// The desk's notifications (port; engine spec §6.3; engine P6 unit (c)): the notifications file and, when one is
/// available, a macOS notification. 'notify.desk' is the only caller (README X5, X50).
/// </summary>
public interface IDeskNotices
{
    /// <summary>Sends <paramref name="notice"/> once: a second send under the same identity writes nothing.</summary>
    /// <param name="notice">The notice.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>True when it was sent now, false when its identity was already sent.</returns>
    public ValueTask<Result<bool>> Send(DeskNotice notice, CancellationToken cancellationToken);
}
