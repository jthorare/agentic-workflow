using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): its writes: Run now, Cancel run, Activate and Pause.</summary>
internal sealed partial class WorkflowsPageViewModel
{
    /// <summary>
    /// Run now: starts a run of the open workflow by hand. The call is sent once (a human action, never retried) and a second press while it is on the way does nothing. A run that
    /// started is opened; a queued or skipped one says so under the button; a refusal shows its message. The runs are read again after any answer that reached Sindri.
    /// </summary>
    /// <param name="cancellationToken">Cancels the call and the reads.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task RunNow(CancellationToken cancellationToken)
    {
        if (_url is not { } url || Runs.IsStarting || !Runs.CanRun || Runs.Slug.Length == 0)
        {
            return;
        }

        var slug = Runs.Slug;
        Runs.SetStarting(starting: true);
        Runs.Note(string.Empty);
        try
        {
            var started = await Api.StartWorkflowRun(url, slug, cancellationToken).ConfigureAwait(true);
            if (!string.Equals(Runs.Slug, slug, StringComparison.Ordinal))
            {
                return;   // the page moved to another workflow meanwhile: its note and list are not this answer's
            }

            if (started.IsError)
            {
                await Refused(url, slug, started.Error, cancellationToken).ConfigureAwait(true);
                return;
            }

            if (string.Equals(started.Value.Status, "started", StringComparison.Ordinal))
            {
                Runs.AddStarting(started.Value.Run);   // at once, from the reply: the server's views row comes later
                Run.ExpectStarting(started.Value.Run);
            }

            await Runs.Read(url, slug, Runs.Title, cancellationToken).ConfigureAwait(true);
            await AfterStart(started.Value, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            Runs.SetStarting(starting: false);
        }
    }

    /// <summary>Retry on a Starting placeholder that did not start: the placeholder goes and Run now runs again.</summary>
    /// <param name="runId">The placeholder's run id.</param>
    /// <param name="cancellationToken">Cancels the call and the reads.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task RetryStart(string runId, CancellationToken cancellationToken) => runId.Length > 0 ? throw new NotSupportedException() : Task.CompletedTask;

    // A refusal says why; one that says the workflow is off turns Run now off. When Sindri didn't answer, the run may have started, so the runs are read
    // again and the note says so: a second press could start a second run.
    private async Task Refused(Uri url, string slug, Error error, CancellationToken cancellationToken)
    {
        if (string.Equals(error.Code, SindriUiErrors.Unreachable.Code, StringComparison.Ordinal))
        {
            Runs.Note(Labels.WfRunNowUnsure(error));
            await Runs.Read(url, slug, Runs.Title, cancellationToken).ConfigureAwait(true);
            return;
        }

        if (string.Equals(error.Code, RunsViewModel.NotActive, StringComparison.Ordinal))
        {
            Runs.TurnOff();
        }

        Runs.Note(Labels.WfRunNowRefused(error));
    }

    // a started run is opened (when the runs are still shown); a queued or skipped one says why under the button
    private async Task AfterStart(WorkflowRunStarted started, CancellationToken cancellationToken)
    {
        switch (started.Status)
        {
            case "started":
                if (_level == Level.Runs)
                {
                    await OpenRun(started.Run, cancellationToken).ConfigureAwait(true);
                }

                break;
            case "queued":
                Runs.Note(Labels.WfRunQueued(started.Reason));
                break;
            default:
                await ShowRunInFlight(started.Reason, cancellationToken).ConfigureAwait(true);
                break;
        }
    }

    // A skipped run means another run of the workflow is live: say so in one line and open that run (the newest live one on the list just read). With none on the list, the skip says why.
    private async Task ShowRunInFlight(string? reason, CancellationToken cancellationToken)
    {
        var live = _level == Level.Runs ? Runs.Rows.FirstOrDefault(r => RunStatus.IsLive(r.Status)) : null;
        if (live is null)
        {
            Runs.Note(Labels.WfRunSkipped(reason));
            return;
        }

        await OpenRun(live.Id, cancellationToken).ConfigureAwait(true);
        Run.Note(Labels.WfRunInFlight);
    }

    /// <summary>
    /// Cancel run: asks the open run to stop. Sent once. "Already ended" is shown in words and the run is read again, so its chip says how it ended; a delivered cancel is read again
    /// too, and the run ends as cancelled when its workflow stops.
    /// </summary>
    /// <param name="cancellationToken">Cancels the call and the read.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task CancelRun(CancellationToken cancellationToken)
    {
        if (_url is not { } url || Run.IsCancelling || !Run.CanCancel)
        {
            return;
        }

        var id = Run.Id;
        Run.SetCancelling(cancelling: true);
        Run.Note(string.Empty);
        try
        {
            var cancelled = await Api.CancelWorkflowRun(url, id, cancellationToken).ConfigureAwait(true);
            var ended = cancelled.IsError && string.Equals(cancelled.Error.Code, RunAlreadyEnded, StringComparison.Ordinal);
            var note = cancelled.IsSuccess ? Labels.WfCancelSent : ended ? Labels.WfCancelEnded : Labels.WfCancelRefused(cancelled.Error);
            if (!string.Equals(Run.Id, id, StringComparison.Ordinal))
            {
                return;   // another run is open now
            }

            Run.Note(note);
            if (cancelled.IsSuccess || ended)
            {
                await Run.Read(url, id, cancellationToken).ConfigureAwait(true);
            }
        }
        finally
        {
            Run.SetCancelling(cancelling: false);
        }
    }

    /// <summary>True while Activate's or Pause's call is on the way; a second press does nothing.</summary>
    public bool IsChangingState { get; private set { Set(ref field, value); OnChanged(nameof(ActivateText)); OnChanged(nameof(PauseText)); NotifyStateButtons(); } }

    /// <summary>What the last Activate or Pause said (refused or unsure), in words; empty otherwise.</summary>
    public string StateNoticeText { get; private set { Set(ref field, value); OnChanged(nameof(HasStateNotice)); } } = string.Empty;

    /// <summary>True when there is a note under the Activate or Pause button.</summary>
    public bool HasStateNotice => StateNoticeText.Length > 0;

    /// <summary>The Activate button's words: "Activate", or "Activating…" while its call is on the way.</summary>
    public string ActivateText => IsChangingState ? Labels.WfActivateBusy : Labels.WfActivate;

    /// <summary>The Pause button's words: "Pause", or "Pausing…" while its call is on the way.</summary>
    public string PauseText => IsChangingState ? Labels.WfPauseBusy : Labels.WfPause;

    /// <summary>True when no call is on the way and the open workflow is off, or an edit made a newer revision than the live one: Activate is offered.</summary>
    public bool CanActivate => IsRuns && !IsChangingState && OpenRow is { IsActive: false } or { HasNewerRevision: true };

    /// <summary>True when the open workflow is on and no call is on the way: Pause is offered.</summary>
    public bool CanPause => IsRuns && !IsChangingState && OpenRow is { IsActive: true };

    private WorkflowRowViewModel? OpenRow => Rows.FirstOrDefault(r => string.Equals(r.Slug, Runs.Slug, StringComparison.Ordinal));

    /// <summary>Activate: turns the open workflow's newest revision on. Sent once; the list and the runs are read again after any answer that reached Sindri.</summary>
    /// <param name="cancellationToken">Cancels the call and the reads.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task Activate(CancellationToken cancellationToken) => ChangeState(CanActivate, ActivateCall, Labels.WfActivateRefused, cancellationToken);

    /// <summary>Pause: turns the open workflow off. Sent once; the list and the runs are read again after any answer that reached Sindri.</summary>
    /// <param name="cancellationToken">Cancels the call and the reads.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task Pause(CancellationToken cancellationToken) => ChangeState(CanPause, PauseCall, Labels.WfPauseRefused, cancellationToken);

    private async Task<Error?> ActivateCall(Uri url, string slug, CancellationToken cancellationToken)
    {
        var activated = await Api.ActivateWorkflow(url, slug, cancellationToken).ConfigureAwait(true);
        return activated.IsError ? activated.Error : null;
    }

    private async Task<Error?> PauseCall(Uri url, string slug, CancellationToken cancellationToken)
    {
        var paused = await Api.PauseWorkflow(url, slug, cancellationToken).ConfigureAwait(true);
        return paused.IsError ? paused.Error : null;
    }

    // One call, never retried. A refusal says why and changes nothing; an answer that reached Sindri, or no answer (the change may have applied), reads the list and the runs again.
    private async Task ChangeState(bool allowed, Func<Uri, string, CancellationToken, Task<Error?>> send, Func<Error, string> refused, CancellationToken cancellationToken)
    {
        if (_url is not { } url || !allowed || Runs.Slug.Length == 0)
        {
            return;
        }

        var slug = Runs.Slug;
        StateNoticeText = string.Empty;
        IsChangingState = true;
        try
        {
            var error = await send(url, slug, cancellationToken).ConfigureAwait(true);
            await LandStateChange(url, slug, error, refused, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            IsChangingState = false;
        }
    }

    // The answer of an activate or pause: the notice (only while that workflow is still shown), and a read again unless Sindri refused.
    private async Task LandStateChange(Uri url, string slug, Error? error, Func<Error, string> refused, CancellationToken cancellationToken)
    {
        var here = string.Equals(Runs.Slug, slug, StringComparison.Ordinal);
        var unsure = error is not null && string.Equals(error.Code, SindriUiErrors.Unreachable.Code, StringComparison.Ordinal);
        if (here && error is not null)
        {
            StateNoticeText = unsure ? Labels.WfStateUnsure(error) : refused(error);
        }

        if (error is null || unsure)
        {
            await ReadList(url, cancellationToken).ConfigureAwait(true);
            if (here)
            {
                await Runs.Read(url, slug, Runs.Title, cancellationToken).ConfigureAwait(true);
            }
        }
    }

    // the buttons depend on the open workflow, the level and the rows: tell the view when any of them moved
    private void NotifyStateButtons()
    {
        OnChanged(nameof(CanActivate));
        OnChanged(nameof(CanPause));
    }

    // another workflow was opened: the old one's note is not about it
    private void ForgetStateNote()
    {
        StateNoticeText = string.Empty;
        NotifyStateButtons();
    }
}
