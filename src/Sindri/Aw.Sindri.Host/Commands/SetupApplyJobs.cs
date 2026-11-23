using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Features.AddRepo;
using Aw.Sindri.Features.GetJob;
using Aw.Sindri.Features.RunService;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The jobs 'sindri setup --apply' starts and waits for, quietly (first-run spec decision 10): one AddRepo job per repo, then a run of the setup service. Each is read every second until it ends;
/// a start refused because the same job runs already (SND-JOB-409 naming it) follows that job instead.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="clock">Times the reads and the setup pass's wait.</param>
internal sealed partial class SetupApplyJobs(ControlPlaneClient client, TimeProvider clock)
{
    /// <summary>How often a job is read while it runs.</summary>
    internal static readonly TimeSpan Poll = TimeSpan.FromSeconds(1);

    /// <summary>Adds one repo and waits for its job: null when it was added, "already" when the profile names it already, otherwise why it couldn't be added.</summary>
    /// <param name="path">The repo's absolute path.</param>
    /// <param name="ct">Cancels the wait.</param>
    /// <returns>Null for added, the error otherwise.</returns>
    internal async Task<Error?> Add(string path, CancellationToken ct)
    {
        var started = await client.Post<AddRepoRequest, JobStartedResponse>("/sindri/setup/repos", new AddRepoRequest(path), ct).ConfigureAwait(false);
        var id = started.IsSuccess ? started.Value.Value.JobId : RunningJob(started.Error);
        if (id is not { } job)
        {
            return started.Error;
        }

        var ended = await Until(job, deadline: null, ct).ConfigureAwait(false);
        if (ended.IsError)
        {
            return ended.Error;
        }

        return ended.Value is not { } view || view.State == JobState.Succeeded
            ? null
            : new Error(view.Code ?? SindriErrors.InvalidInput.Code, ErrorKind.Invalid, Line(view.Summary ?? "the job ended without a summary"));
    }

    /// <summary>True when <paramref name="error"/> says the folder is already a repo of the profile (SND-SETUP-002 for the same path, not a taken name).</summary>
    /// <param name="error">Why the add didn't happen.</param>
    /// <returns>True for "already there".</returns>
    internal static bool Already(Error error) =>
        string.Equals(error.Code, SindriErrors.SetupRepoExists.Code, StringComparison.Ordinal) && error.Message.Contains("already the repo", StringComparison.Ordinal);

    /// <summary>Runs the setup service now and waits for it up to <paramref name="timeout"/> (zero: start it and don't wait).</summary>
    /// <param name="timeout">How long to wait.</param>
    /// <param name="ct">Cancels the wait.</param>
    /// <returns>How the pass ended.</returns>
    internal async Task<SetupPassOutcome> Pass(TimeSpan timeout, CancellationToken ct)
    {
        var start = clock.GetUtcNow();
        var deadline = start + timeout;
        var id = await StartPass(deadline, ct).ConfigureAwait(false);
        if (id.IsError)
        {
            return NotStarted(id.Error, timeout, start);
        }

        if (timeout == TimeSpan.Zero)
        {
            return new SetupPassOutcome(SetupPassOutcome.Skipped, id.Value.Value, Seconds: 0);
        }

        var job = id.Value.Value;
        var ended = await Until(id.Value, deadline, ct).ConfigureAwait(false);
        if (ended.IsError)
        {
            return new SetupPassOutcome(SetupPassOutcome.Failed, job, Seconds(start)) { Error = Line(ended.Error.Message) };
        }

        return ended.Value switch
        {
            null => new SetupPassOutcome(SetupPassOutcome.Timeout, job, Seconds(start)),
            { State: JobState.Succeeded } => new SetupPassOutcome(SetupPassOutcome.Done, job, Seconds(start)),
            var view => new SetupPassOutcome(SetupPassOutcome.Failed, job, Seconds(start)) { Error = Line(view.Summary ?? view.Code ?? "the job ended without a summary") },
        };
    }

    // No job to follow. A caller that may not run services: refused. A scheduled pass still in flight (SND-JOB-409 naming no job): the pass runs, so --timeout 0 skips the wait
    // and any other wait timed out. Anything else failed.
    private SetupPassOutcome NotStarted(Error error, TimeSpan timeout, DateTimeOffset start)
    {
        if (Refused(error))
        {
            return new SetupPassOutcome(SetupPassOutcome.Refused, JobId: null, Seconds: 0);
        }

        if (!string.Equals(error.Code, SindriErrors.JobConflict.Code, StringComparison.Ordinal))
        {
            return new SetupPassOutcome(SetupPassOutcome.Failed, JobId: null, Seconds(start)) { Error = Line(error.Message) };
        }

        return timeout == TimeSpan.Zero
            ? new SetupPassOutcome(SetupPassOutcome.Skipped, JobId: null, Seconds: 0)
            : new SetupPassOutcome(SetupPassOutcome.Timeout, JobId: null, Seconds(start));
    }

    // The pass's job: the one this start made, or the one already running (SND-JOB-409 naming it). A scheduled pass in flight (409 naming no job) is waited out, one try a second, until the deadline.
    private async Task<Result<JobId>> StartPass(DateTimeOffset deadline, CancellationToken ct)
    {
        while (true)
        {
            var started = await client.Post<RunServiceRequest, JobStartedResponse>("/sindri/services/setup/run", body: null, ct).ConfigureAwait(false);
            if (started.IsSuccess)
            {
                return started.Value.Value.JobId;
            }

            if (RunningJob(started.Error) is { } running)
            {
                return running;
            }

            if (!string.Equals(started.Error.Code, SindriErrors.JobConflict.Code, StringComparison.Ordinal) || clock.GetUtcNow() >= deadline)
            {
                return started.Error;
            }

            await Task.Delay(Poll, clock, ct).ConfigureAwait(false);
        }
    }

    // The job when it ended, null when the deadline came first, or the error of a read.
    private async Task<Result<JobView?>> Until(JobId id, DateTimeOffset? deadline, CancellationToken ct)
    {
        while (true)
        {
            var read = await client.GetView<JobView>("/sindri/jobs/" + Uri.EscapeDataString(id.Value), ct).ConfigureAwait(false);
            if (read.IsError)
            {
                return read.Error;
            }

            if (read.Value.Value.State is not (JobState.Running or JobState.Waiting))
            {
                return read.Value.Value;
            }

            if (deadline is { } end && clock.GetUtcNow() >= end)
            {
                return Aw.Kernel.Results.Success<JobView?>(value: null);
            }

            await Task.Delay(Poll, clock, ct).ConfigureAwait(false);
        }
    }

    // SND-JOB-409 names the job that runs already in its message ('sindri jobs show JOB-…'); null for any other error or a 409 naming none.
    private static JobId? RunningJob(Error error) =>
        string.Equals(error.Code, SindriErrors.JobConflict.Code, StringComparison.Ordinal) && JobIdText.Match(error.Message) is { Success: true } found ? JobId.From(found.Value) : null;

    // A caller that may not run services: the scheduler runs the pass on its next tick.
    private static bool Refused(Error error) => error.Kind == ErrorKind.Forbidden;

    private int Seconds(DateTimeOffset start) => (int)Math.Round((clock.GetUtcNow() - start).TotalSeconds, MidpointRounding.AwayFromZero);

    // Free text from a job or the server, fenced as untrusted: one line, escaped (first-run spec §5.1).
    private static string Line(string text) => SafeText.Line(SafeText.Escape(text.Split('\n')[0]), 200);

    [GeneratedRegex("JOB-[0-9a-f]{32}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex JobIdText { get; }
}
