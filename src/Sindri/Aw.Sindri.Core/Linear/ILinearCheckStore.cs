using Aw.Kernel;

namespace Aw.Sindri.Core.Linear;

/// <summary>Keeps, across restarts, the result and time of the last save-time check of each Linear key. Holds no key and no secret.</summary>
public interface ILinearCheckStore
{
    /// <summary>Reads the stored checks.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The checks; a missing or unreadable store is two nulls, never an error.</returns>
    public ValueTask<Result<LinearStoredChecks>> Read(CancellationToken cancellationToken);

    /// <summary>Stores the read key's check, replacing the last one.</summary>
    /// <param name="check">The check.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success; an error when the file can't be written.</returns>
    public ValueTask<Result<Unit>> RecordRead(LinearStoredCheck check, CancellationToken cancellationToken);

    /// <summary>Stores the write key's check, replacing the last one.</summary>
    /// <param name="check">The check.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success; an error when the file can't be written.</returns>
    public ValueTask<Result<Unit>> RecordWrite(LinearStoredCheck check, CancellationToken cancellationToken);

    /// <summary>Forgets the write key's check.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, also when there was none.</returns>
    public ValueTask<Result<Unit>> ClearWrite(CancellationToken cancellationToken);
}
