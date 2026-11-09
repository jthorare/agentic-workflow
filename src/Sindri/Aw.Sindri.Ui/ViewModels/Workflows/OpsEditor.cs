using System.Collections.ObjectModel;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// The workflow editor's logic (#2258 PR 17 part 2b), no view yet: it reads a workflow and the block list, builds the form of the selected node from its block's config fields, and
/// sends each edit as ops with the revision it read as the base. Each edit is sent once and never retried; a workflow that changed elsewhere is not overwritten but asks for a reload.
/// </summary>
internal sealed class OpsEditor(Aw.Sindri.Ui.Api.ISindriApi api) : ObservableObject
{
    /// <summary>The workflow's slug; empty until loaded.</summary>
    public string Slug { get; private set; } = string.Empty;

    /// <summary>The newest revision read: the base every edit is sent on.</summary>
    public int BaseRevision { get; private set; }

    /// <summary>The nodes of the workflow as read.</summary>
    public IReadOnlyList<EditorNode> Nodes { get; private set; } = [];

    /// <summary>The block types by group, as the picker shows them.</summary>
    public IReadOnlyList<WorkflowBlockGroup> Groups { get; private set; } = [];

    /// <summary>The form of the selected node.</summary>
    public ObservableCollection<ConfigFieldViewModel> Fields { get; } = [];

    /// <summary>True while an edit is on the way; a second edit does nothing.</summary>
    public bool IsSending { get; private set; }

    /// <summary>True when the workflow changed elsewhere: edits are held back until Reload.</summary>
    public bool NeedsReload { get; private set; }

    /// <summary>The prompt under the editor when the workflow changed elsewhere; empty otherwise.</summary>
    public string ReloadPromptText { get; private set; } = string.Empty;

    /// <summary>The Reload command's words.</summary>
    public string ReloadText => Labels.WfReload;

    /// <summary>Why the last edit or read failed (not a stale base), in words; empty otherwise.</summary>
    public string FailureText { get; private set; } = string.Empty;

    /// <summary>Reads a workflow and the block list.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="slug">The workflow.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when both are read.</returns>
    internal Task Load(Uri url, string slug, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Reads the workflow again (the Reload command): the held edit is dropped, not retried.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the workflow is read.</returns>
    internal Task Reload(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Builds the form of a node from its block's config fields.</summary>
    /// <param name="nodeId">The node.</param>
    internal void Select(string nodeId) => throw new NotSupportedException();

    /// <summary>Sends a set_config for one field of the selected node.</summary>
    /// <param name="name">The field.</param>
    /// <param name="text">The new text; empty on an optional field removes the key.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task EditField(string name, string text, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Sends an add_node.</summary>
    /// <param name="id">The new node's id.</param>
    /// <param name="type">Its block type.</param>
    /// <param name="title">Its title, or null for none.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task AddNode(string id, string type, string? title, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Sends a connect.</summary>
    /// <param name="from">The node the edge leaves.</param>
    /// <param name="to">The node it enters.</param>
    /// <param name="output">The branch it leaves by, or null for the default.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task Connect(string from, string to, string? output, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Sends a remove_node.</summary>
    /// <param name="id">The node.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task RemoveNode(string id, CancellationToken cancellationToken) => throw new NotSupportedException();
}
