namespace Aw.Sindri.Core.Ledger;

/// <summary>
/// The ledger's append signal (API spec §5.5): the ledger adapter pulses after each committed append, so the projector
/// and every event stream wake within milliseconds instead of waiting out their 1 s poll.
/// </summary>
public interface ILedgerSignal
{
    /// <summary>Tells every waiter that <paramref name="sequence"/> is committed.</summary>
    /// <param name="sequence">The sequence just committed.</param>
    public void Pulse(long sequence);

    /// <summary>
    /// Waits for a pulse past <paramref name="sequence"/>: returns at once when one already came, else when one comes, else
    /// after <paramref name="limit"/> with the latest sequence known.
    /// </summary>
    /// <param name="sequence">The waiter's cursor.</param>
    /// <param name="limit">The longest wait.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The latest sequence known.</returns>
    public ValueTask<long> WaitAfter(long sequence, TimeSpan limit, CancellationToken cancellationToken);
}
