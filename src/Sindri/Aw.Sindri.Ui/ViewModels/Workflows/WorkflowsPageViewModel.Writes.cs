using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): its two writes, Run now and Cancel run.</summary>
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

            await Runs.Read(url, slug, Runs.Title, cancellationToken).ConfigureAwait(true);
            await AfterStart(started.Value, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            Runs.SetStarting(starting: false);
        }
    }

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
                Runs.Note(Labels.WfRunSkipped(started.Reason));
                break;
        }
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
}
