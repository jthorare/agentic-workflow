using System.Collections.Specialized;
using System.ComponentModel;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): the editor sheet (#2258 PR 17 part 3a). The sheet shows the form of the node selected on the graph; Save sends each changed field as its own edit.</summary>
internal sealed partial class WorkflowsPageViewModel
{
    /// <summary>The editor over the page's API.</summary>
    internal OpsEditor Editor { get; }

    /// <summary>True while the editor sheet is open.</summary>
    public bool IsEditing { get; private set => Set(ref field, value); }

    /// <summary>True when the open workflow can be edited: a workflow is open on its runs and it is not archived.</summary>
    public bool CanEdit => IsRuns && Runs.Shown is { ArchivedAt: null };

    /// <summary>The Edit button's words.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string EditText => Labels.WfEdit;

    /// <summary>The Close button's words.</summary>
    public string CloseEditorText => Labels.WfEditClose;

    /// <summary>The line shown when the form has no fields.</summary>
    public string NoFieldsText => Labels.WfEditorNoFields;
#pragma warning restore CA1822, MA0041

    /// <summary>The sheet's heading: the workflow and the revision the edits are sent on.</summary>
    public string EditorHeading => Labels.WfEditorHeading(Runs.Title, Editor.BaseRevision);

    /// <summary>The line naming the node whose settings are shown; empty when none is selected.</summary>
    public string EditorNodeText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the form has no fields.</summary>
    public bool HasNoFields => Editor.Fields.Count == 0;

    /// <summary>"Save", or "Saving..." while an edit is on the way.</summary>
    public string SaveText => Editor.IsSending ? Labels.WfEditSaving : Labels.WfEditSave;

    /// <summary>True when Save does something: the form has fields, no edit is on the way and no reload is due.</summary>
    public bool CanSave => Editor.Fields.Count > 0 && !Editor.IsSending && !Editor.NeedsReload;

    /// <summary>Opens the sheet on the selected graph node's fields.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the form is made.</returns>
    internal async Task Edit(CancellationToken cancellationToken)
    {
        if (_url is not { } url || !CanEdit)
        {
            return;
        }

        IsEditing = true;
        var node = Runs.Graph?.SelectedId ?? string.Empty;
        EditorNodeText = node.Length == 0 ? string.Empty : Labels.WfEditorNode(node);
        await Editor.Load(url, Runs.Slug, cancellationToken).ConfigureAwait(true);
        Editor.Select(node);
    }

    /// <summary>Sends the fields that changed, one edit each, on the revision read. A field with a problem stops the save before anything is sent; a refusal or a stale answer stops it after.</summary>
    /// <param name="cancellationToken">Cancels the calls.</param>
    /// <returns>A task that completes when the answers are shown.</returns>
    internal async Task Save(CancellationToken cancellationToken)
    {
        if (!CanSave)
        {
            return;
        }

        var changed = Editor.Fields.Where(f => f.IsChanged).Select(f => (f.Name, f.Text)).ToList();
        var blocked = false;
        foreach (var field in Editor.Fields.Where(f => f.IsChanged))
        {
            field.ErrorText = EditorFieldText.Parse(field, field.Text).Problem;
            blocked |= field.HasError;
        }

        if (blocked)
        {
            return;
        }

        foreach (var (name, text) in changed)
        {
            await Editor.EditField(name, text, cancellationToken).ConfigureAwait(true);
            if (Editor.NeedsReload || Editor.FailureText.Length > 0)
            {
                return;   // the rest waits: a stale base is reloaded first, a refusal is read first
            }
        }
    }

    /// <summary>Closes the sheet.</summary>
    internal void CloseEditor() => IsEditing = false;

    private void WatchEditor()
    {
        Editor.PropertyChanged += OnEditorChanged;
        Editor.Fields.CollectionChanged += OnFieldsChanged;
        Runs.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(RunsViewModel.Graph), StringComparison.Ordinal) || string.Equals(e.PropertyName, nameof(RunsViewModel.IsReading), StringComparison.Ordinal))
            {
                OnChanged(nameof(CanEdit));
                OnChanged(nameof(EditorHeading));
            }
        };
    }

    private void OnEditorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(OpsEditor.BaseRevision), StringComparison.Ordinal))
        {
            OnChanged(nameof(EditorHeading));
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
    }
}
