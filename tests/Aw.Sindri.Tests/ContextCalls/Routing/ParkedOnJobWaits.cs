using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.Jobs;

namespace Aw.Sindri.Tests.ContextCalls.Routing;

/// <summary>A job's park handle over the real <see cref="JobWaits"/>, as the runner's own is, without its ledger events: ResumeJob wakes it through <see cref="IJobSignals"/>.</summary>
/// <param name="waits">Where the job waits.</param>
/// <param name="job">The job.</param>
internal sealed class ParkedOnJobWaits(JobWaits waits, JobId job) : IJobWaiting
{
    public async ValueTask<bool> WaitFor(string signal, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var wait = waits.Park(job, signal);
        try
        {
            await wait.Came.Task.WaitAsync(timeout, waits.Clock, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        finally
        {
            waits.Unpark(job);
        }
    }
}
