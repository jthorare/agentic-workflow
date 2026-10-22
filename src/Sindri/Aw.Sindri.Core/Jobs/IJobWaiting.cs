namespace Aw.Sindri.Core.Jobs;

/// <summary>What the code running a job can do to park it on a named signal (setup-once spec §5.6): the job shows as 'waiting', holds no job slot, and runs again when the signal comes.</summary>
public interface IJobWaiting
{
    /// <summary>Parks the job on <paramref name="signal"/>: records 'job.waiting', then waits.</summary>
    /// <param name="signal">The signal's name, for example 'onyx-account'.</param>
    /// <param name="timeout">How long to wait at most.</param>
    /// <param name="cancellationToken">Trips on CancelJob or the host stopping; the wait then throws OperationCanceledException.</param>
    /// <returns>True when the signal came ('job.resumed' is recorded); false when the time ran out.</returns>
    public ValueTask<bool> WaitFor(string signal, TimeSpan timeout, CancellationToken cancellationToken);
}
