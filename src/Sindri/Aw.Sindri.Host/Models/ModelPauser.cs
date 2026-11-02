using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Host.Models;

/// <summary>
/// The pause of a job whose every model harness is limited (model routing spec §5.3): the progress line, the park on 'model-limit' for up to 24 hours, and on a resume the job's limited set cleared so the route is walked again from its first
/// harness. One pause per job at a time: a second call of the job that reaches the pause waits behind the first, and walks again if the first was resumed meanwhile.
/// </summary>
/// <param name="limits">The job's limited set and its pause lock.</param>
/// <param name="recorder">Writes the call's event before the job parks.</param>
internal sealed class ModelPauser(JobModelLimits limits, ModelCallRecorder recorder)
{
    private const string Signal = "model-limit";

    private static readonly TimeSpan _wait = TimeSpan.FromHours(24);

    /// <summary>Parks the job.</summary>
    /// <param name="call">The call that found every harness limited.</param>
    /// <param name="job">The job it runs under.</param>
    /// <param name="waiting">The job's park handle.</param>
    /// <param name="cancellationToken">Trips on CancelJob.</param>
    /// <returns>True when the route may be walked again; false when the wait ended without a resume (cancel, or the 24 hours).</returns>
    internal async ValueTask<bool> Park(ModelCall call, JobContext job, IJobWaiting waiting, CancellationToken cancellationToken)
    {
        var entry = limits.EntryFor(job);
        var seen = entry.Resumes;
        try
        {
            await entry.Pause.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }

        try
        {
            return entry.Resumes != seen || await ParkHeld(call, job, waiting, cancellationToken).ConfigureAwait(false);   // a resume that came while this call waited for its turn needs no park
        }
        catch (OperationCanceledException)
        {
            return false;   // CancelJob: the stalled call returns 240
        }
        finally
        {
            _ = entry.Pause.Release();
        }
    }

    private async ValueTask<bool> ParkHeld(ModelCall call, JobContext job, IJobWaiting waiting, CancellationToken cancellationToken)
    {
        await recorder.Write(call, answer: null).ConfigureAwait(false);
        job.Progress.Report(ModelPauseText.Line(ModelCallErrors.Causes(call)));
        if (!await waiting.WaitFor(Signal, _wait, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var entry = limits.EntryFor(job);
        entry.Harnesses.Clear();
        entry.Resumed();
        return true;
    }
}
