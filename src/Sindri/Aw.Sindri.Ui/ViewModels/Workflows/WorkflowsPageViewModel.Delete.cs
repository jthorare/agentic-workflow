using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): Delete, the last item of the header's ⋯ menu (Workflows page spec §4.9, #3306). It always opens one confirm
/// with three forms: off (Delete, red), active (Pause, then the first form in place) and a run going (Open the run). The call is sent once; after it the page returns to the list,
/// reads it again and says what was kept and freed. The server decides: a 114 or 115 answer turns the confirm into that form.
/// </summary>
internal sealed partial class WorkflowsPageViewModel
{
    private const string DeleteActiveCode = "SND-GRAPH-114";
    private const string DeleteInFlightCode = "SND-GRAPH-115";

    private enum DeleteForm
    {
        Closed,
        Off,
        Active,
        InFlight,
    }

    private DeleteForm _deleteForm = DeleteForm.Closed;

    /// <summary>True when a workflow's runs are shown: the ⋯ menu offers Delete.</summary>
    public bool CanDelete => IsRuns && OpenRow is not null;

    /// <summary>True while the confirm is open.</summary>
    public bool IsConfirmingDelete => _deleteForm != DeleteForm.Closed;

    /// <summary>True for the off form: Cancel and a red Delete.</summary>
    public bool IsDeleteOffForm => _deleteForm == DeleteForm.Off;

    /// <summary>True for the active form: Cancel and Pause.</summary>
    public bool IsDeleteActiveForm => _deleteForm == DeleteForm.Active;

    /// <summary>True for the in-flight form: Cancel and Open the run.</summary>
    public bool IsDeleteInFlightForm => _deleteForm == DeleteForm.InFlight;

    /// <summary>The confirm's sentence for its form, naming the workflow.</summary>
    public string DeleteConfirmText => _deleteForm switch
    {
        DeleteForm.Off => Labels.WfDeleteConfirm(Runs.Title),
        DeleteForm.Active => Labels.WfDeleteActive(Runs.Title),
        DeleteForm.InFlight => Labels.WfDeleteInFlight(Runs.Title),
        _ => string.Empty,
    };

    /// <summary>The red button's words: "Delete", or "Deleting…" while its call is on the way.</summary>
    public string DeleteYesText => IsDeleting ? Labels.WfDeleteBusy : Labels.WfDeleteYes;

    /// <summary>The ⋯ menu item's words.</summary>
    public string DeleteMenuText => Labels.WfDelete;

    /// <summary>The ⋯ button's accessible name.</summary>
    public string MoreText => Labels.WfMore;

    /// <summary>The confirm's Cancel.</summary>
    public string DeleteCancelText => Labels.WfDeleteCancel;

    /// <summary>The active form's Pause.</summary>
    public string DeletePauseText => IsChangingState ? Labels.WfPauseBusy : Labels.WfDeletePause;

    /// <summary>The in-flight form's Open the run.</summary>
    public string DeleteOpenRunText => Labels.WfDeleteOpenRun;

    /// <summary>True while Delete's call is on the way: the red button shows "Deleting…" and is off, and a second press does nothing.</summary>
    public bool IsDeleting { get; private set { Set(ref field, value); OnChanged(nameof(DeleteYesText)); OnChanged(nameof(CanConfirmDelete)); } }

    /// <summary>True when the red button can be pressed.</summary>
    public bool CanConfirmDelete => IsDeleteOffForm && !IsDeleting;

    /// <summary>What the confirm says when Delete was refused for another reason or got no answer; empty otherwise.</summary>
    public string DeleteNoteText { get; private set { Set(ref field, value); OnChanged(nameof(HasDeleteNote)); } } = string.Empty;

    /// <summary>True when the confirm holds a refusal.</summary>
    public bool HasDeleteNote => DeleteNoteText.Length > 0;

    /// <summary>The list's notice after a delete: "Deleted nightly-size. 12 runs kept, 1.2 MB freed", and the resources that stayed. No Undo.</summary>
    public string DeletedNoticeText { get; private set { Set(ref field, value); OnChanged(nameof(HasDeletedNotice)); } } = string.Empty;

    /// <summary>True when the list shows the notice.</summary>
    public bool HasDeletedNotice => DeletedNoticeText.Length > 0;

    /// <summary>The run the in-flight form opens, from the 115 answer; empty otherwise.</summary>
    public string DeleteRunId { get; private set; } = string.Empty;

    /// <summary>Opens the confirm in the form the open workflow's row says (active or off). Sends nothing.</summary>
    internal void RequestDelete()
    {
        if (!CanDelete)
        {
            return;
        }

        DeleteNoteText = string.Empty;
        SetDeleteForm(OpenRow is { IsActive: true } ? DeleteForm.Active : DeleteForm.Off);
    }

    /// <summary>Closes the confirm (Cancel, Esc). Nothing is deleted.</summary>
    internal void CancelDelete()
    {
        DeleteNoteText = string.Empty;
        SetDeleteForm(DeleteForm.Closed);
    }

    /// <summary>
    /// The red Delete (or Ctrl+Enter) in the off form: sends the delete once. A success returns to the list, reads it again and shows the notice; a 114 or 115 turns the confirm
    /// into that form; any other refusal is said in the confirm.
    /// </summary>
    /// <param name="cancellationToken">Cancels the call and the read.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task ConfirmDelete(CancellationToken cancellationToken)
    {
        if (_url is not { } url || !CanConfirmDelete || Runs.Slug.Length == 0)
        {
            return;
        }

        var slug = Runs.Slug;
        DeleteNoteText = string.Empty;
        IsDeleting = true;
        try
        {
            var deleted = await Api.DeleteWorkflow(url, slug, cancellationToken).ConfigureAwait(true);
            if (!string.Equals(Runs.Slug, slug, StringComparison.Ordinal) || !IsConfirmingDelete)
            {
                return;   // the page moved on meanwhile: this answer's confirm is gone
            }

            if (deleted.IsError)
            {
                await DeleteRefused(url, deleted.Error, cancellationToken).ConfigureAwait(true);
                return;
            }

            var gone = deleted.Value;
            SetDeleteForm(DeleteForm.Closed);
            SetLevel(Level.List);
            DeletedNoticeText = Labels.WfDeleted(gone.Slug, gone.RunsKept, gone.BytesFreed, gone.ResourcesStayed);
            await ReadList(url, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            IsDeleting = false;
        }
    }

    /// <summary>The active form's Pause: pauses the workflow, then shows the off form in place. The owner still presses Delete.</summary>
    /// <param name="cancellationToken">Cancels the call and the reads.</param>
    /// <returns>A task that completes when the form is shown.</returns>
    internal async Task PauseFromDelete(CancellationToken cancellationToken)
    {
        if (!IsDeleteActiveForm)
        {
            return;
        }

        await Pause(cancellationToken).ConfigureAwait(true);
        if (IsDeleteActiveForm && OpenRow is { IsActive: false })
        {
            SetDeleteForm(DeleteForm.Off);
        }
    }

    /// <summary>The in-flight form's Open the run: closes the confirm and opens the run that is still going.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the run is shown.</returns>
    internal Task OpenDeleteRun(CancellationToken cancellationToken)
    {
        if (!IsDeleteInFlightForm || DeleteRunId.Length == 0)
        {
            return Task.CompletedTask;
        }

        var run = DeleteRunId;
        SetDeleteForm(DeleteForm.Closed);
        return OpenRun(run, cancellationToken);
    }

    // 114: the workflow is active after all (the list was stale), so the list is read again and the Pause form shows; 115: the run form, naming the run;
    // anything else is said in the confirm, and no answer from Sindri reads the list again (the delete may have applied).
    private async Task DeleteRefused(Uri url, Error error, CancellationToken cancellationToken)
    {
        if (string.Equals(error.Code, DeleteActiveCode, StringComparison.Ordinal))
        {
            SetDeleteForm(DeleteForm.Active);
            await ReadList(url, cancellationToken).ConfigureAwait(true);
            return;
        }

        if (string.Equals(error.Code, DeleteInFlightCode, StringComparison.Ordinal))
        {
            DeleteRunId = RunIdIn(error.Message);
            SetDeleteForm(DeleteForm.InFlight);
            return;
        }

        DeleteNoteText = string.Equals(error.Code, SindriUiErrors.Unreachable.Code, StringComparison.Ordinal) ? Labels.WfStateUnsure(error) : Labels.WfDeleteRefused(error);
        if (string.Equals(error.Code, SindriUiErrors.Unreachable.Code, StringComparison.Ordinal))
        {
            await ReadList(url, cancellationToken).ConfigureAwait(true);
        }
    }

    // the 115 message names the run as 'slug@revision:occurrence'
    private static string RunIdIn(string message) => RunIdPattern().Match(message) is { Success: true } found ? found.Groups[1].Value : string.Empty;

    [GeneratedRegex("'([a-z0-9-]+@[0-9]+:[0-9]+)'", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RunIdPattern();

    private void SetDeleteForm(DeleteForm form)
    {
        if (form != DeleteForm.InFlight)
        {
            DeleteRunId = string.Empty;
        }

        _deleteForm = form;
        OnChanged(nameof(IsConfirmingDelete));
        OnChanged(nameof(IsDeleteOffForm));
        OnChanged(nameof(IsDeleteActiveForm));
        OnChanged(nameof(IsDeleteInFlightForm));
        OnChanged(nameof(DeleteConfirmText));
        OnChanged(nameof(CanConfirmDelete));
        OnChanged(nameof(DeleteRunId));
    }

    // another workflow was opened or the level moved: the confirm was about the old one, and the list's notice is old news once a workflow opens
    private void ForgetDelete()
    {
        DeleteNoteText = string.Empty;
        DeletedNoticeText = string.Empty;
        SetDeleteForm(DeleteForm.Closed);
        OnChanged(nameof(CanDelete));
    }
}
