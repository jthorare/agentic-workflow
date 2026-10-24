using Aw.Kernel;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Approvals;

/// <summary>The approve-all dialog (<see cref="ApproveAllViewModel"/>, continued): the approval sent once, the job followed, the result in words.</summary>
internal sealed partial class ApproveAllViewModel
{
    /// <summary>True while the approval is on its way to Sindri.</summary>
    public bool IsSending { get; private set; }

    /// <summary>What the job is on now, from its latest line; empty before.</summary>
    public string Progress { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>What the approval did, once it ended; null before.</summary>
    public ApproveAllResult? Result { get; private set => Set(ref field, value); }

    /// <summary>"3 workflows approved" once the approval ended; empty before.</summary>
    public string Outcome { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The workflows the checks refused, each with its sentence and the command that approves it on its own.</summary>
    public IReadOnlyList<ApproveAllLine> Refused { get; private set => Set(ref field, value); } = [];

    /// <summary>The workflows left because one they wait on was refused.</summary>
    public IReadOnlyList<ApproveAllLine> Skipped { get; private set => Set(ref field, value); } = [];

    /// <summary>True when something was refused.</summary>
    public bool HasRefused => Refused.Count > 0;

    /// <summary>True when something was skipped.</summary>
    public bool HasSkipped => Skipped.Count > 0;

    // The approval goes once: the guard is set before the first await.
    private async Task Redeem(ApproveAllRequest request)
    {
        if (IsSending || _url() is not { } api)
        {
            return;
        }

        MarkSending();
        var session = _session;
        Result<ApproveAllStarted> started;
        try
        {
            started = await _api.ApproveAll(api, request, CancellationToken.None).ConfigureAwait(true);
        }
        finally
        {
            if (session == _session)   // a closed or reopened dialog has its own guard
            {
                IsSending = false;
                OnChanged(nameof(IsSending));
            }
        }

        if (session != _session)
        {
            return;
        }

        if (started.IsError)
        {
            await OnRefused(api, started.Error, session).ConfigureAwait(true);
            return;
        }

        Progress = Labels.ApproveRunning(string.Empty);
        Phase = ApproveAllPhase.Applying;
        await Follow(api, started.Value.JobId, session).ConfigureAwait(true);
    }

    // The approval is on its way: the guard says so, and the last refusal is cleared.
    private void MarkSending()
    {
        IsSending = true;
        (Error, ErrorCode, Notice) = (string.Empty, string.Empty, string.Empty);
        OnChanged(nameof(CanApprove));
        OnChanged(nameof(IsSending));
    }

    // A refusal is shown in words; the plan having changed reads it again.
    private async Task OnRefused(Uri api, Error error, int session)
    {
        if (string.Equals(error.Code, PlanChangedRefusal, StringComparison.Ordinal))
        {
            var read = await _api.ApproveAllPlan(api, CancellationToken.None).ConfigureAwait(true);
            if (session == _session)
            {
                ShowPlan(read);
                Notice = Labels.ApprovePlanChanged;
            }

            return;
        }

        Fail(error, keepPlan: true);
    }

    // Reads the job until it ends. A few failed reads in a row, or two minutes, end the wait with words; closing the dialog (or opening again) ends it silently.
    private async Task Follow(Uri api, string jobId, int session)
    {
        var until = _clock.GetUtcNow() + _pollFor;
        var failures = 0;
        while (session == _session)
        {
            var job = await _api.ApproveAllJob(api, jobId, CancellationToken.None).ConfigureAwait(true);
            if (session != _session)
            {
                return;
            }

            failures = job.IsError ? failures + 1 : 0;
            var ended = await Step(api, job, failures, session).ConfigureAwait(true);
            if (ended || await TimedOut(until).ConfigureAwait(true))
            {
                return;
            }
        }
    }

    // One read of the job: true when the dialog has nothing more to wait for.
    private async Task<bool> Step(Uri api, Result<ApproveAllJob> job, int failures, int session)
    {
        if (failures >= ReadFailuresAllowed)
        {
            Fail(new Error("SND-UI-002", ErrorKind.Unavailable, Labels.ApproveLostTrack));
            return true;
        }

        if (job.IsError)
        {
            return false;
        }

        if (InProgress(job.Value))
        {
            Progress = Labels.ApproveRunning(job.Value.Progress is { Count: > 0 } lines ? lines[^1].Line : string.Empty);
            return false;
        }

        await Finish(api, job.Value, session).ConfigureAwait(true);
        return true;
    }

    // Past the deadline the wait ends with words; otherwise it sleeps one beat.
    private async Task<bool> TimedOut(DateTimeOffset until)
    {
        if (_clock.GetUtcNow() >= until)
        {
            Fail(new Error("SND-UI-003", ErrorKind.Timeout, Labels.ApproveStillRunning));
            return true;
        }

        await Task.Delay(_pollEvery, _clock).ConfigureAwait(true);
        return false;
    }

    private static bool InProgress(ApproveAllJob job) => job.State.Equals("running", StringComparison.Ordinal) || job.State.Equals("waiting", StringComparison.Ordinal) || job.State.Equals("queued", StringComparison.Ordinal);

    // The job ended. With its result the dialog tells what happened; without one it failed, in the job's words.
    private async Task Finish(Uri api, ApproveAllJob job, int session)
    {
        if (job.Result is not { } result)
        {
            var words = job.State.Equals("cancelled", StringComparison.Ordinal) ? Labels.ApproveCancelled : job.Summary ?? Labels.ApproveLostTrack;
            Fail(new Error(job.Code ?? "SND-UI-003", ErrorKind.Unavailable, words));
            return;
        }

        Result = result;
        Outcome = Labels.ApproveOutcome(result.Approved.Count);
        Refused = [.. result.Refused.Select(r => new ApproveAllLine(NameOf(r.Id), r.Sentence, Cli: r.Cli ?? string.Empty))];
        Skipped = [.. result.Skipped.Select(s => new ApproveAllLine(NameOf(s.Id, s.DisplayName), Labels.ApproveSkipped(NameOf(s.Id, s.DisplayName), NameOf(s.Because))))];
        OnChanged(nameof(HasRefused));
        OnChanged(nameof(HasSkipped));
        Phase = ApproveAllPhase.Done;
        Live.Say(Outcome);
        await ReadAgain(api, session).ConfigureAwait(true);
    }

    // The plan again (what the approval left): the own-look list stays honest. A failed read changes nothing.
    private async Task ReadAgain(Uri api, int session)
    {
        var read = await _api.ApproveAllPlan(api, CancellationToken.None).ConfigureAwait(true);
        if (session == _session && read.IsSuccess)
        {
            OwnApproval = [.. read.Value.OwnApproval.Select(OwnLine)];
            OnChanged(nameof(HasOwnApproval));
        }
    }

    // A workflow's words: the plan's display name for it, else the one the result gave, else the id made readable.
    private string NameOf(string id, string? served = null) =>
        WorkflowNames.Of(id, _plan?.Recipes.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.Ordinal))?.DisplayName ?? served);
}
