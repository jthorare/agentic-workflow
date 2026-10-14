namespace Aw.Sindri.Core.Ledger;

/// <summary>The in-process <see cref="ILedgerSignal"/>: one per process, registered by AddSindriCore.</summary>
/// <param name="clock">Times the wait's limit.</param>
internal sealed class LedgerSignal(TimeProvider clock) : ILedgerSignal
{
    internal TimeProvider Clock { get; } = clock;

    public void Pulse(long sequence)
        => throw new NotSupportedException("LedgerSignal.Pulse: P5 unit C makes LedgerSignalContractTests pass");

    public ValueTask<long> WaitAfter(long sequence, TimeSpan limit, CancellationToken cancellationToken)
        => throw new NotSupportedException("LedgerSignal.WaitAfter: P5 unit C makes LedgerSignalContractTests pass");
}
