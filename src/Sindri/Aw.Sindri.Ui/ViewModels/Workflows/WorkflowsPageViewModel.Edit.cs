using System.Collections.Specialized;
using System.ComponentModel;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): the editor sheet (#2258 PR 17 part 3a). The sheet shows the form of the node selected on the graph; Save sends every changed field in one call on the revision read.</summary>
internal sealed partial class WorkflowsPageViewModel
{
    private int _edits;   // counts Edit calls: only the newest one selects its node

    /// <summary>The editor over the page's API.</summary>
    internal OpsEditor Editor { get; }

    /// <summary>True in graph edit mode (Edit pressed, Done not yet): the edit bar shows and works. The step settings sheet is separate, see <see cref="IsSettingsOpen"/>.</summary>
    public bool IsEditing { get; private set { Set(ref field, value); OnChanged(nameof(ShowsEdit)); } }

    /// <summary>True while the step settings sheet is open. It locks the edit bar, so the sheet's typed text is never reloaded under the person.</summary>
    public bool IsSettingsOpen { get; private set { Set(ref field, value); OnChanged(nameof(StepSummaryText)); OnChanged(nameof(HasStepSummary)); OnChanged(nameof(CanOpenSettings)); OnChanged(nameof(ShowsStepRow)); } }

    /// <summary>True when Step settings is offered: a step is selected, the sheet is shut and the workflow can be edited.</summary>
    public bool CanOpenSettings => !IsSettingsOpen && CanEdit && Runs.Graph?.SelectedId is not null;

    /// <summary>The selected step's title and summary, shown under the graph while the sheet is shut; empty otherwise.</summary>
    public string StepSummaryText => IsSettingsOpen ? string.Empty : Runs.Graph?.StepLine ?? string.Empty;

    /// <summary>True when the line under the graph shows.</summary>
    public bool HasStepSummary => StepSummaryText.Length > 0;

    /// <summary>True when the row under the graph shows: a step summary or Step settings.</summary>
    public bool ShowsStepRow => HasStepSummary || CanOpenSettings;

    /// <summary>True when the open workflow can be edited: a workflow is open on its runs and it is not archived.</summary>
    public bool CanEdit => IsRuns && Runs.Shown is { ArchivedAt: null };

    /// <summary>True when the Edit button shows: the workflow can be edited and edit mode is off (Done is the way out while it is on).</summary>
    public bool ShowsEdit => CanEdit && !IsEditing;

    /// <summary>The Edit button's words.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string EditText => Labels.WfEdit;

    /// <summary>The Done button's words.</summary>
    public string DoneText => Labels.WfEditDone;

    /// <summary>The Step settings button's words.</summary>
    public string StepSettingsText => Labels.WfStepSettings;

    /// <summary>The Close button's words.</summary>
    public string CloseEditorText => Labels.WfEditClose;

    /// <summary>The line shown when the form has no fields.</summary>
    public string NoFieldsText => Labels.WfEditorNoFields;
#pragma warning restore CA1822, MA0041

    /// <summary>The sheet's heading: the selected step and its kind, or the workflow's name when none is selected.</summary>
    public string EditorHeading => Editor.NodeHeading.Length > 0 ? Editor.NodeHeading : Labels.WfEditorHeading(Runs.Title);

    /// <summary>True when the form has no fields.</summary>
    public bool HasNoFields => Editor.Fields.Count == 0;

    /// <summary>"Save", or "Saving..." while an edit is on the way.</summary>
    public string SaveText => Editor.IsSending ? Labels.WfEditSaving : Labels.WfEditSave;

    /// <summary>True when Save does something: the form has fields read for the workflow the page shows (and it is not archived), no edit is on the way and no reload is due.</summary>
    public bool CanSave => Editor.Fields.Count > 0 && !Editor.IsSending && !Editor.NeedsReload && CanEdit && Editor.BaseRevision > 0 && string.Equals(Editor.Slug, Runs.Slug, StringComparison.Ordinal);

    /// <summary>Turns graph edit mode on: the edit bar shows and works, the sheet stays shut.</summary>
    internal void Edit()
    {
        if (CanEdit)
        {
            IsEditing = true;
        }
    }

    /// <summary>Opens the step settings sheet on the selected graph node's fields (and turns edit mode on).</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the form is made.</returns>
    internal async Task OpenSettings(CancellationToken cancellationToken)
    {
        if (_url is not { } url || !CanEdit)
        {
            return;
        }

        (IsEditing, IsSettingsOpen, GraphEdit.SheetOpen) = (true, true, true);
        var (edit, slug) = (++_edits, Runs.Slug);
        var node = Runs.Graph?.SelectedId ?? string.Empty;
        await Editor.Load(url, slug, cancellationToken).ConfigureAwait(true);
        if (edit != _edits || !IsSettingsOpen || !string.Equals(Editor.Slug, slug, StringComparison.Ordinal))
        {
            return;   // a newer Edit started or the sheet closed while this one read: its node belongs to an older sheet
        }

        Editor.Select(node);
    }

    /// <summary>Sends every changed field in ONE call on the revision read: all are saved or none, and a refusal or a stale answer keeps every edit in the form. A field with a problem stops the save before anything is sent.</summary>
    /// <param name="cancellationToken">Cancels the calls.</param>
    /// <returns>A task that completes when the answers are shown.</returns>
    internal async Task Save(CancellationToken cancellationToken)
    {
        if (!IsSettingsOpen || !CanSave)
        {
            return;
        }

        var changed = Editor.Fields.Where(f => f.IsChanged).Select(f => (f.Name, f.Text)).ToList();
        var slug = Runs.Slug;
        await Editor.EditFields(changed, cancellationToken).ConfigureAwait(true);
        await RereadRuns(slug, cancellationToken).ConfigureAwait(true);   // the page shows the revision Sindri holds now, so a graph edit next is not held as "never showed"
    }

    /// <summary>Reads the workflow again from the sheet's Reload button.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the workflow is read.</returns>
    internal async Task ReloadEditor(CancellationToken cancellationToken)
    {
        var slug = Runs.Slug;
        await Editor.Reload(cancellationToken).ConfigureAwait(true);
        await RereadRuns(slug, cancellationToken).ConfigureAwait(true);
    }

    private async Task RereadRuns(string slug, CancellationToken cancellationToken)
    {
        if (_url is { } url && slug.Length > 0 && string.Equals(Runs.Slug, slug, StringComparison.Ordinal))
        {
            await Runs.Read(url, slug, Runs.Title, cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>Closes the sheet; edit mode stays on.</summary>
    internal void CloseSettings() => (IsSettingsOpen, GraphEdit.SheetOpen) = (false, false);

    /// <summary>True while Done asks "Save your changes?" because a field of the open sheet changed.</summary>
    public bool AsksSaveFirst { get; private set => Set(ref field, value); }

    /// <summary>The question's words.</summary>
    public string AskSaveText => Labels.WfAskSave;

    /// <summary>The question's Discard button.</summary>
    public string AskDiscardText => Labels.WfAskDiscard;

    /// <summary>The question's Keep editing button.</summary>
    public string AskKeepText => Labels.WfAskKeep;

    private bool _leaveWhenSaved;

    /// <summary>Done (and Escape in edit mode with the sheet shut): leaves edit mode, or asks first when a field of the open sheet changed. A save in flight is waited for, and its result decides.</summary>
    internal void RequestDone()
    {
        if (Editor.IsSending)
        {
            _leaveWhenSaved = true;
            return;
        }

        if (IsSettingsOpen && Editor.Fields.Any(static f => f.IsChanged))
        {
            AsksSaveFirst = true;
            return;
        }

        CloseEditor();
    }

    /// <summary>The question's Discard: leaves edit mode and drops the typed fields.</summary>
    internal void DiscardAndLeave()
    {
        AsksSaveFirst = false;
        CloseEditor();
    }

    /// <summary>The question's Keep editing: back to the sheet, text kept.</summary>
    internal void KeepEditing() => AsksSaveFirst = false;

    /// <summary>The question's Save: saves, and leaves edit mode only when the save worked; a failed save keeps the sheet and its error.</summary>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>A task that completes when the save is answered.</returns>
    internal async Task SaveAndLeave(CancellationToken cancellationToken)
    {
        AsksSaveFirst = false;
        await Save(cancellationToken).ConfigureAwait(true);
        LeaveIfSaved();
    }

    // The save worked when the sheet shows no failure and no reload prompt.
    private void LeaveIfSaved()
    {
        if (Editor.FailureText.Length == 0 && !Editor.NeedsReload)
        {
            CloseEditor();
        }
    }

    /// <summary>Closes the sheet and leaves edit mode (Done, or leaving the workflow).</summary>
    internal void CloseEditor()
    {
        CloseSettings();
        IsEditing = false;
        GraphEdit.Cancel();   // a half-made Add or Connect does not wait for the next Edit
    }

    private void WatchEditor()
    {
        Editor.PropertyChanged += OnEditorChanged;
        Editor.Fields.CollectionChanged += OnFieldsChanged;
        Runs.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(RunsViewModel.Graph), StringComparison.Ordinal) || string.Equals(e.PropertyName, nameof(RunsViewModel.IsReading), StringComparison.Ordinal) || string.Equals(e.PropertyName, nameof(RunsViewModel.Shown), StringComparison.Ordinal))
            {
                OnChanged(nameof(CanEdit));
                OnChanged(nameof(ShowsEdit));
                OnChanged(nameof(DetailStateText));
                OnChanged(nameof(ShowsDetailState));
                OnChanged(nameof(CanOpenSettings));
                OnChanged(nameof(CanSave));
                OnChanged(nameof(EditorHeading));
                OnChanged(nameof(StepSummaryText));
                OnChanged(nameof(HasStepSummary));
                OnChanged(nameof(ShowsStepRow));
                if (string.Equals(e.PropertyName, nameof(RunsViewModel.Graph), StringComparison.Ordinal) && !ReferenceEquals(_watchedGraph, Runs.Graph))
                {
                    _watchedGraph?.PropertyChanged -= OnGraphChanged;   // the old workflow's graph stops reaching this page
                    _watchedGraph = Runs.Graph;
                    _watchedGraph?.PropertyChanged += OnGraphChanged;
                }
            }
        };
    }

    // The graph whose StepLine and SelectedId this page follows; swapped, never stacked, when another workflow opens.
    private WorkflowGraphViewModel? _watchedGraph;

    private void OnGraphChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(WorkflowGraphViewModel.StepLine), StringComparison.Ordinal))
        {
            OnChanged(nameof(StepSummaryText));
            OnChanged(nameof(HasStepSummary));
            OnChanged(nameof(ShowsStepRow));
        }

        if (string.Equals(e.PropertyName, nameof(WorkflowGraphViewModel.SelectedId), StringComparison.Ordinal))
        {
            OnChanged(nameof(CanOpenSettings));
            OnChanged(nameof(ShowsStepRow));
        }
    }

    private void OnEditorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(OpsEditor.BaseRevision), StringComparison.Ordinal))
        {
            OnChanged(nameof(CanSave));
        }
        else if (string.Equals(e.PropertyName, nameof(OpsEditor.NodeHeading), StringComparison.Ordinal))
        {
            OnChanged(nameof(EditorHeading));
        }
        else if (string.Equals(e.PropertyName, nameof(OpsEditor.Slug), StringComparison.Ordinal))
        {
            OnChanged(nameof(CanSave));
        }
        else if (string.Equals(e.PropertyName, nameof(OpsEditor.IsSending), StringComparison.Ordinal))
        {
            OnChanged(nameof(SaveText));
            OnChanged(nameof(CanSave));
            if (!Editor.IsSending && _leaveWhenSaved)
            {
                _leaveWhenSaved = false;   // Done was pressed during the save: its result decides
                LeaveIfSaved();
            }
        }
        else if (string.Equals(e.PropertyName, nameof(OpsEditor.NeedsReload), StringComparison.Ordinal))
        {
            OnChanged(nameof(CanSave));
        }
    }

    private void OnFieldsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnChanged(nameof(HasNoFields));
        OnChanged(nameof(CanSave));
    }
}
