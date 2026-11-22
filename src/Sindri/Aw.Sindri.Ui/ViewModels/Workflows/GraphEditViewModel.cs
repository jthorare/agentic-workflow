using System.ComponentModel;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// The graph edits of the open workflow (#2258 PR 17 part 3b): Add step, Remove and Connect on the selected node, each sent through <see cref="OpsEditor"/> as ops on the revision it read.
/// Add step opens the block picker, Remove asks first (naming the node), Connect lists the other nodes. After any sent edit the page reads the workflow again, so the graph shows the new
/// revision; a workflow that changed elsewhere shows the editor's reload prompt and every command stays off until Reload.
/// </summary>
internal sealed partial class GraphEditViewModel : ObservableObject
{
    private static readonly string[] _watchedNames =
    [
        nameof(CanEdit), nameof(CanAdd), nameof(CanRemove), nameof(CanConnect), nameof(Groups), nameof(Targets), nameof(ReloadPromptText), nameof(HasReloadPrompt), nameof(FailureText), nameof(HasFailure),
        nameof(IsAdding), nameof(IsConnecting), nameof(IsConfirmingRemove), nameof(ConfirmText), nameof(SheetOpen), nameof(SheetOpenText),
    ];

    private readonly RunsViewModel _runs;
    private readonly Func<Uri?> _url;
    private WorkflowGraphViewModel? _watched;
    private Mode _mode;
    private string _slug = string.Empty;

    internal GraphEditViewModel(OpsEditor editor, RunsViewModel runs, Func<Uri?> url)
    {
        (Editor, _runs, _url) = (editor, runs, url);
        editor.PropertyChanged += (_, _) => NotifyAll();
        runs.PropertyChanged += (_, _) =>
        {
            if (!string.Equals(runs.Slug, _slug, StringComparison.Ordinal))
            {
                (_slug, _mode) = (runs.Slug, Mode.None);   // another workflow: a picker, list or confirm open for the old one would send to the new one
            }

            Watch(runs.Graph);
            NotifyAll();
        };
        Watch(runs.Graph);
    }

    /// <summary>The editor the ops go through.</summary>
    public OpsEditor Editor { get; }

    /// <summary>True when an edit may be sent now: a workflow is open and not archived, no edit is on the way, the editor sheet is closed and the workflow did not change elsewhere.</summary>
    public bool CanEdit => _url() is not null && _runs.Slug.Length > 0 && _runs.Graph is not null && _runs.Shown is { ArchivedAt: null } && !Editor.IsSending && !Editor.NeedsReload && !SheetOpen;

    /// <summary>True when Add step is offered.</summary>
    public bool CanAdd => CanEdit;

    /// <summary>True when Remove is offered: a node is selected.</summary>
    public bool CanRemove => CanEdit && Selected is not null;

    /// <summary>True when Connect to is offered: a node is selected and another one exists.</summary>
    public bool CanConnect => CanEdit && Selected is not null && _runs.Graph!.Nodes.Count > 1;   // why: a selected node means a graph

    /// <summary>True while the block picker is open.</summary>
    public bool IsAdding => _mode == Mode.Add;

    /// <summary>True while the target list is open.</summary>
    public bool IsConnecting => _mode == Mode.Connect;

    /// <summary>True while the remove confirm is open.</summary>
    public bool IsConfirmingRemove => _mode == Mode.Remove;

    /// <summary>The remove confirm's question, naming the node; empty when it is not open.</summary>
    public string ConfirmText => _mode == Mode.Remove && Selected is { } node ? Labels.WfEditConfirmRemove(node.Title) : string.Empty;

    /// <summary>The block picker's groups.</summary>
    public IReadOnlyList<WorkflowBlockGroup> Groups => Editor.Groups;

    /// <summary>The nodes a connect may go to: every node but the selected one.</summary>
    public IReadOnlyList<ConnectTarget> Targets =>
        _runs.Graph is { } graph ? [.. graph.Nodes.Where(n => !string.Equals(n.Id, graph.SelectedId, StringComparison.Ordinal)).Select(n => new ConnectTarget(n.Id, Labels.WfEditTarget(n.Title, n.Id)))] : [];

    /// <summary>The prompt when the workflow changed elsewhere; empty otherwise.</summary>
    public string ReloadPromptText => Editor.ReloadPromptText;

    /// <summary>True when the reload prompt is shown.</summary>
    public bool HasReloadPrompt => Editor.NeedsReload;

    /// <summary>Why the last edit failed (not a stale base), in words; empty otherwise.</summary>
    public string FailureText => Editor.FailureText;

    /// <summary>True when a failure line is shown.</summary>
    public bool HasFailure => Editor.FailureText.Length > 0;

    private GraphNodeViewModel? Selected => _runs.Graph is { SelectedId: { } id } graph ? graph.Nodes.FirstOrDefault(n => string.Equals(n.Id, id, StringComparison.Ordinal)) : null;

    /// <summary>Opens the block picker; the block list is read first.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the picker is ready.</returns>
    internal async Task BeginAdd(CancellationToken cancellationToken)
    {
        if (!CanAdd || !await EnsureLoaded(cancellationToken).ConfigureAwait(true))
        {
            return;
        }

        SetMode(Editor.Groups.Count > 0 ? Mode.Add : Mode.None);
    }

    /// <summary>Set by the page: opens the editor sheet on a new step (the block, the id it gets, the node it follows). Nothing is sent until the sheet's Save.</summary>
    internal Action<WorkflowBlock, string, string?>? OpenNewStep { get; set; }

    /// <summary>Starts a step of a block: the editor sheet opens on its settings, pre-filled with the block's example, placed after the selected node when one is selected. Nothing is sent; Save in the sheet sends one add_node.</summary>
    /// <param name="block">The block.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the sheet is open.</returns>
    internal async Task AddStep(WorkflowBlock block, CancellationToken cancellationToken)
    {
        if (!CanAdd || !await EnsureLoaded(cancellationToken).ConfigureAwait(true) || !StillSame)
        {
            return;
        }

        var after = _runs.Graph?.SelectedId;
        SetMode(Mode.None);
        OpenNewStep?.Invoke(block, FreshId(block.Type), after);
    }

    /// <summary>Asks to remove the selected node: the confirm names it, nothing is sent yet.</summary>
    internal void RequestRemove()
    {
        if (CanRemove)
        {
            SetMode(Mode.Remove);
        }
    }

    /// <summary>Removes the node the confirm named.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task ConfirmRemove(CancellationToken cancellationToken)
    {
        if (_mode != Mode.Remove || !CanRemove || Selected is not { } node || !await EnsureLoaded(cancellationToken).ConfigureAwait(true) || !StillSame)
        {
            return;
        }

        await Send(() => Editor.RemoveNode(node.Id, cancellationToken), cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Opens the list of nodes the selected one may connect to.</summary>
    internal void BeginConnect()
    {
        if (CanConnect)
        {
            SetMode(Mode.Connect);
        }
    }

    /// <summary>Connects the selected node to another one.</summary>
    /// <param name="id">The other node.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task ConnectTo(string id, CancellationToken cancellationToken)
    {
        if (!CanConnect || _runs.Graph?.SelectedId is not { } from || Targets.All(t => !string.Equals(t.Id, id, StringComparison.Ordinal)) || !await EnsureLoaded(cancellationToken).ConfigureAwait(true) || !StillSame)
        {
            return;
        }

        await Send(() => Editor.Connect(from, id, output: null, cancellationToken), cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Closes the picker, the target list or the confirm.</summary>
    internal void Cancel() => SetMode(Mode.None);

    /// <summary>Reads the workflow again after it changed elsewhere: the held edit is dropped, and the page shows the new revision.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the page shows the new revision.</returns>
    internal async Task Reload(CancellationToken cancellationToken)
    {
        SetMode(Mode.None);
        await Editor.Reload(cancellationToken).ConfigureAwait(true);
        await ReadPage(_runs.Slug, cancellationToken).ConfigureAwait(true);
    }

    // The reads inside EnsureLoaded awaited: the person may have opened the sheet, the workflow may have been archived or another one opened since the command was allowed.
    private bool StillSame => CanEdit && string.Equals(_runs.Slug, Editor.Slug, StringComparison.Ordinal);

    // The editor holds the revision its edits are sent on: it reads the workflow again when it has none for this one, or when the page shows another revision than it read.
    private async Task<bool> EnsureLoaded(CancellationToken cancellationToken)
    {
        if (_url() is not { } url || _runs.Slug.Length == 0)
        {
            return false;
        }

        if (!string.Equals(Editor.Slug, _runs.Slug, StringComparison.Ordinal) || Editor.BaseRevision != _runs.Shown?.LatestRevision || Editor.Groups.Count == 0)
        {
            await Editor.Load(url, _runs.Slug, cancellationToken).ConfigureAwait(true);
            if (await HoldOnUnseenRevision(cancellationToken).ConfigureAwait(true))
            {
                return false;
            }
        }

        return Editor.Groups.Count > 0 && Editor.BaseRevision > 0;
    }

    // The workflow moved on since the page read it: nothing is sent on a revision the person never saw, and the page reads it again.
    private async Task<bool> HoldOnUnseenRevision(CancellationToken cancellationToken)
    {
        if (Editor.BaseRevision == 0 || Editor.BaseRevision == _runs.Shown?.LatestRevision)
        {
            return false;
        }

        Editor.MarkChangedElsewhere();
        SetMode(Mode.None);
        await ReadPage(_runs.Slug, cancellationToken).ConfigureAwait(true);
        return true;
    }

    // One edit, never retried; whatever it answered, the page reads the workflow again so the graph shows the revision Sindri holds.
    private async Task Send(Func<Task> edit, CancellationToken cancellationToken)
    {
        var slug = _runs.Slug;
        await edit().ConfigureAwait(true);
        SetMode(Mode.None);
        await ReadPage(slug, cancellationToken).ConfigureAwait(true);
    }

    private async Task ReadPage(string slug, CancellationToken cancellationToken)
    {
        if (_url() is { } url && slug.Length > 0 && string.Equals(_runs.Slug, slug, StringComparison.Ordinal))
        {
            await _runs.Read(url, slug, _runs.Title, cancellationToken).ConfigureAwait(true);
        }
    }

    private void SetMode(Mode mode)
    {
        _mode = mode;
        NotifyAll();
    }

    private void Watch(WorkflowGraphViewModel? graph)
    {
        if (ReferenceEquals(_watched, graph))
        {
            return;
        }

        if (_watched is { } old)
        {
            old.PropertyChanged -= OnGraphChanged;
            old.RemoveRequested -= OnRemoveRequested;
        }

        _watched = graph;
        if (graph is not null)
        {
            graph.PropertyChanged += OnGraphChanged;
            graph.RemoveRequested += OnRemoveRequested;
        }
    }

    // another node was selected: a list or confirm open for the old one is closed
    private void OnGraphChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(WorkflowGraphViewModel.SelectedId), StringComparison.Ordinal) && _mode is Mode.Connect or Mode.Remove)
        {
            _mode = Mode.None;
        }

        NotifyAll();
    }

    private void OnRemoveRequested(object? sender, ValueEventArgs<string> e) => RequestRemove();

    private void NotifyAll()
    {
        foreach (var name in _watchedNames)
        {
            OnChanged(name);
        }
    }
}
