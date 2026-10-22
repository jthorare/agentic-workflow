using System.Text;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.GetJob;

namespace Aw.Sindri.Host.Commands;

/// <summary>Reads the 'job.*' frames of the event stream, and a job view, as the words the follower prints (API P6 unit H).</summary>
internal static class JobFrames
{
    /// <summary>The signal frame <paramref name="frame"/> carries for <paramref name="job"/>, or null for another job's frame, another type or a payload that doesn't read.</summary>
    /// <param name="output">Reads the JSON.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="job">The job being followed (the stream carries every job's events: the filter is client-side).</param>
    /// <returns>The signal, or null.</returns>
    internal static JobSignal? Read(Output output, StreamFrame frame, JobId job)
    {
        if (frame.Event is not (EventTypes.JobProgress or EventTypes.JobFinished or EventTypes.JobCancelled))
        {
            return null;
        }

        var parsed = output.Read<PublicEvent>(Encoding.UTF8.GetBytes(frame.Data));
        if (parsed.IsError)
        {
            return null;
        }

        var payload = Encoding.UTF8.GetBytes(parsed.Value.Payload.GetRawText());
        return frame.Event switch
        {
            EventTypes.JobProgress => Progress(output.Read<JobProgress>(payload), job),
            EventTypes.JobFinished => Finished(output.Read<JobFinishedPublic>(payload), job),
            _ => Cancelled(output.Read<JobCancelled>(payload), job),
        };
    }

    /// <summary>The end a job view in a final state means; null while it runs.</summary>
    /// <param name="view">The job.</param>
    /// <returns>The end, or null.</returns>
    internal static JobSignal? Ended(JobView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return view.State switch
        {
            JobState.Running or JobState.Waiting => null,
            JobState.Cancelled => new JobSignal(Round: 0, "cancelled", Exit: 1, Ended: true),
            JobState.Succeeded => End(JobOutcome.Succeeded, view.Summary ?? string.Empty, view.Code),
            JobState.Failed => End(JobOutcome.Failed, view.Summary ?? string.Empty, view.Code),
            _ => null,
        };
    }

    private static JobSignal? Progress(Kernel.Result<JobProgress> read, JobId job) =>
        read.IsSuccess && read.Value.Job == job ? new JobSignal(read.Value.Round, read.Value.Line, Exit: 0, Ended: false) : null;

    private static JobSignal? Finished(Kernel.Result<JobFinishedPublic> read, JobId job) =>
        read.IsSuccess && read.Value.Job == job ? End(read.Value.Outcome, read.Value.Summary, read.Value.Code) : null;

    private static JobSignal? Cancelled(Kernel.Result<JobCancelled> read, JobId job) =>
        read.IsSuccess && read.Value.Job == job ? new JobSignal(Round: 0, "cancelled", Exit: 1, Ended: true) : null;

    private static JobSignal End(JobOutcome outcome, string summary, string? code) => outcome == JobOutcome.Succeeded
        ? new JobSignal(Round: 0, "succeeded: " + summary, Exit: 0, Ended: true)
        : new JobSignal(Round: 0, "failed: " + summary + (code is null ? string.Empty : " " + code), Exit: 1, Ended: true);
}
