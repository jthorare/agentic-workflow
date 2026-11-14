using System.Collections.Specialized;
using System.ComponentModel;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): the editor sheet (#2258 PR 17 part 3a). The sheet shows the form of the node selected on the graph; Save sends every changed field in one call on the revision read.</summary>
internal sealed partial class WorkflowsPageViewModel
{
    private int _edits;   // counts Edit calls: only the newest one selects its node

    /// <summary>The editor over the page's API.</summary>
    internal OpsEditor Editor { get; }

    /// <summary>True while the editor sheet is open.</summary>
    public bool IsEditing { get; private set { Set(ref field, value); OnChanged(nameof(StepSummaryText)); OnChanged(nameof(HasStepSummary)); } }

    /// <summary>The selected step's title and summary, shown under the graph while Edit is off; empty otherwise.</summary>
    public string StepSummaryText => IsEditing ? string.Empty : Runs.Graph?.StepLine ?? string.Empty;

    /// <summary>True when the line under the graph shows.</summary>
    public bool HasStepSummary => StepSummaryText.Length > 0;

    /// <summary>True when the open workflow can be edited: a workflow is open on its runs and it is not archived.</summary>
    public bool CanEdit => IsRuns && Runs.Shown is { ArchivedAt: null };

    /// <summary>The Edit button's words.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string EditText => Labels.WfEdit;

    /// <summary>The Close button's words.</summary>
    public string CloseEditorText => Labels.WfEditClose;

    /// <summary>The line shown when the form has no fields.</summary>
    public string NoFieldsText => IsNewStep ? Labels.WfEditorNewStepNoFields : Labels.WfEditorNoFields;
#pragma warning restore CA1822, MA0041

    /// <summary>The sheet's heading: the selected step and its kind, or the workflow's name when none is selected.</summary>
    public string EditorHeading => Editor.NodeHeading.Length > 0 ? Editor.NodeHeading : Labels.WfEditorHeading(Runs.Title);

    /// <summary>True when the form has no fields.</summary>
    public bool HasNoFields => Editor.Fields.Count == 0;

    /// <summary>"Save", or "Saving..." while an edit is on the way.</summary>
    public string SaveText => Editor.IsSending ? Labels.WfEditSaving : Labels.WfEditSave;

    /// <summary>True when Save does something: the form has fields read for the workflow the page shows (and it is not archived), no edit is on the way and no reload is due.</summary>
    public bool CanSave => IsNewStep ? Editor.RequiredFilled && CanSend : Editor.Fields.Count > 0 && CanSend;

    // no edit is on the way, no reload is due, and the workflow the page shows is the one the editor read
    private bool CanSend => !Editor.IsSending && !Editor.NeedsReload && CanEdit && Editor.BaseRevision > 0 && string.Equals(Editor.Slug, Runs.Slug, StringComparison.Ordinal);

    /// <summary>Opens the sheet on the selected graph node's fields.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the form is made.</returns>
    internal async Task Edit(CancellationToken cancellationToken)
    {
        if (_url is not { } url || !CanEdit)
        {
            return;
        }

        Editor.ClearPendingStep();   // Edit on a node leaves a new step that was being filled in
        OnChanged(nameof(IsNewStep));
        OnChanged(nameof(NoFieldsText));
        (IsEditing, GraphEdit.SheetOpen) = (true, true);
        var (edit, slug) = (++_edits, Runs.Slug);
        var node = Runs.Graph?.SelectedId ?? string.Empty;
        await Editor.Load(url, slug, cancellationToken).ConfigureAwait(true);
        if (edit != _edits || !IsEditing || !string.Equals(Editor.Slug, slug, StringComparison.Ordinal))
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
        if (!IsEditing || !CanSave)
        {
            return;
        }

        if (IsNewStep)
        {
            var slugNew = Runs.Slug;
            var added = await Editor.AddPendingStep(cancellationToken).ConfigureAwait(true);
            await RereadRuns(slugNew, cancellationToken).ConfigureAwait(true);
            if (added)
            {
                CloseEditor();   // the step exists now; a refusal keeps the sheet open with the refusal shown and every typed value
            }

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

    /// <summary>Closes the sheet.</summary>
    internal void CloseEditor()
    {
        Editor.ClearPendingStep();   // Cancel on a new step: nothing was sent, nothing is added
        (IsEditing, GraphEdit.SheetOpen) = (false, false);
        OnChanged(nameof(IsNewStep));
        OnChanged(nameof(NoFieldsText));
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
                OnChanged(nameof(CanSave));
                OnChanged(nameof(EditorHeading));
                OnChanged(nameof(StepSummaryText));
                OnChanged(nameof(HasStepSummary));
                if (string.Equals(e.PropertyName, nameof(RunsViewModel.Graph), StringComparison.Ordinal) && Runs.Graph is { } graph)
                {
                    graph.PropertyChanged += OnGraphChanged;
                }
            }
        };
    }

    private void OnGraphChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(WorkflowGraphViewModel.StepLine), StringComparison.Ordinal))
        {
            OnChanged(nameof(StepSummaryText));
            OnChanged(nameof(HasStepSummary));
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
        foreach (var field in e.NewItems?.OfType<ConfigFieldViewModel>() ?? [])
        {
            field.PropertyChanged += (_, _) => OnChanged(nameof(CanSave));   // a new step's Save turns on as its required fields are filled
        }
    }
}
