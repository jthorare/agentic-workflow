using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Host.Onyx;

/// <summary>
/// `onyx up`'s progress while the Onyx setup runs as a job: each line goes to the job's own lines (cleaned, bounded and recorded by the job runtime) instead of standard error, and a dot is dropped. Outside a job it is the console's.
/// </summary>
/// <param name="console">Where a line goes when no job runs.</param>
/// <param name="jobs">The job whose work is running now, if any.</param>
internal sealed class JobOnyxProgress(IOnyxProgress console, IJobContextAccessor jobs) : IOnyxProgress
{
    /// <inheritdoc />
    public void Line(string text)
    {
        if (jobs.Current is { } job)
        {
            job.Progress.Report(text);
        }
        else
        {
            console.Line(text);
        }
    }

    /// <inheritdoc />
    public void Tick()
    {
        if (jobs.Current is null)
        {
            console.Tick();
        }
    }
}
